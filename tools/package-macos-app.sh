#!/usr/bin/env bash

# to publish: tools/package-macos-app.sh --runtime osx-x64
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

project_path="$repo_root/src/TriloGame.Game/TriloGame.Game.csproj"
configuration="Release"
runtime="osx-arm64"
publish_root="$repo_root/artifacts/publish"
app_root="$repo_root/artifacts/apps"
app_name="Exuvia"
bundle_id="com.trilobites.game"
self_contained="true"
skip_publish="false"
sign_identity="-"
skip_sign="false"

usage() {
  cat <<'USAGE'
Usage: tools/package-macos-app.sh [options]

Options:
  --runtime <rid>           macOS runtime identifier: osx-arm64 or osx-x64.
                            Default: osx-arm64
  --configuration <name>    Build configuration used for publish.
                            Default: Release
  --project <path>          Path to the game .csproj.
                            Default: src/TriloGame.Game/TriloGame.Game.csproj
  --publish-root <path>     Root folder for dotnet publish output.
                            Default: artifacts/publish
  --app-root <path>         Root folder for generated .app bundles.
                            Default: artifacts/apps
  --app-name <name>         Generated app bundle name.
                            Default: Exuvia
  --bundle-id <id>          CFBundleIdentifier value.
                            Default: com.trilobites.game
  --self-contained <bool>   Passed to dotnet publish.
                            Default: true
  --sign-identity <name>    Code signing identity. Use '-' for ad-hoc signing.
                            Default: -
  --skip-sign               Do not code sign the generated .app bundle.
  --skip-publish            Package an existing publish folder.
  -h, --help                Show this help.

Examples:
  tools/package-macos-app.sh --runtime osx-arm64
  tools/package-macos-app.sh --runtime osx-x64 --app-name "Trilobites Intel"
  tools/package-macos-app.sh --skip-publish --publish-root artifacts/publish
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --runtime)
      runtime="${2:?Missing value for --runtime}"
      shift 2
      ;;
    --configuration)
      configuration="${2:?Missing value for --configuration}"
      shift 2
      ;;
    --project)
      project_path="${2:?Missing value for --project}"
      shift 2
      ;;
    --publish-root)
      publish_root="${2:?Missing value for --publish-root}"
      shift 2
      ;;
    --app-root)
      app_root="${2:?Missing value for --app-root}"
      shift 2
      ;;
    --app-name)
      app_name="${2:?Missing value for --app-name}"
      shift 2
      ;;
    --bundle-id)
      bundle_id="${2:?Missing value for --bundle-id}"
      shift 2
      ;;
    --self-contained)
      self_contained="${2:?Missing value for --self-contained}"
      shift 2
      ;;
    --sign-identity)
      sign_identity="${2:?Missing value for --sign-identity}"
      shift 2
      ;;
    --skip-sign)
      skip_sign="true"
      shift
      ;;
    --skip-publish)
      skip_publish="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

case "$runtime" in
  osx-arm64|osx-x64)
    ;;
  *)
    echo "Unsupported runtime '$runtime'. Use osx-arm64 or osx-x64." >&2
    exit 2
    ;;
esac

if [[ "$project_path" != /* ]]; then
  project_path="$repo_root/$project_path"
fi

if [[ "$publish_root" != /* ]]; then
  publish_root="$repo_root/$publish_root"
fi

if [[ "$app_root" != /* ]]; then
  app_root="$repo_root/$app_root"
fi

publish_dir="$publish_root/$runtime"
app_dir="$app_root/$runtime/$app_name.app"
contents_dir="$app_dir/Contents"
macos_dir="$contents_dir/MacOS"
resources_dir="$contents_dir/Resources"
launcher_path="$macos_dir/$app_name"
launcher_source="$macos_dir/${app_name}Launcher.c"
game_executable="$macos_dir/TriloGame.Game"

if [[ ! -f "$project_path" ]]; then
  echo "Could not find project file: $project_path" >&2
  exit 1
fi

if [[ "$skip_publish" != "true" ]]; then
  echo "Publishing $runtime build to $publish_dir..."
  dotnet publish "$project_path" \
    -c "$configuration" \
    -r "$runtime" \
    --self-contained "$self_contained" \
    -o "$publish_dir"
fi

if [[ ! -d "$publish_dir" ]]; then
  echo "Could not find publish directory: $publish_dir" >&2
  echo "Run without --skip-publish, or publish the game first." >&2
  exit 1
fi

if [[ ! -f "$publish_dir/TriloGame.Game" ]]; then
  echo "Could not find published executable: $publish_dir/TriloGame.Game" >&2
  exit 1
fi

echo "Creating app bundle at $app_dir..."
rm -rf "$app_dir"
mkdir -p "$macos_dir" "$resources_dir"

cp -R "$publish_dir"/. "$macos_dir/"
chmod +x "$game_executable"

if [[ -d "$macos_dir/Content" ]]; then
  cp -R "$macos_dir/Content" "$resources_dir/Content"
else
  echo "Could not find published Content directory: $publish_dir/Content" >&2
  exit 1
fi

if ! command -v clang >/dev/null 2>&1; then
  echo "Could not find clang, which is required to build the native .app launcher." >&2
  echo "Install Xcode Command Line Tools with: xcode-select --install" >&2
  exit 1
fi

cat > "$launcher_source" <<'LAUNCHER'
#include <limits.h>
#include <mach-o/dyld.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

int main(int argc, char **argv)
{
    char executablePath[PATH_MAX];
    uint32_t executablePathSize = sizeof(executablePath);

    if (_NSGetExecutablePath(executablePath, &executablePathSize) != 0)
    {
        fputs("App bundle launcher path is too long.\n", stderr);
        return 1;
    }

    char realExecutablePath[PATH_MAX];
    if (realpath(executablePath, realExecutablePath) == NULL)
    {
        perror("realpath");
        return 1;
    }

    char *lastSlash = strrchr(realExecutablePath, '/');
    if (lastSlash == NULL)
    {
        fputs("Could not locate app bundle MacOS directory.\n", stderr);
        return 1;
    }

    *lastSlash = '\0';
    if (chdir(realExecutablePath) != 0)
    {
        perror("chdir");
        return 1;
    }

    char **gameArgv = calloc((size_t)argc + 1, sizeof(char *));
    if (gameArgv == NULL)
    {
        perror("calloc");
        return 1;
    }

    gameArgv[0] = "./TriloGame.Game";
    for (int i = 1; i < argc; i++)
    {
        gameArgv[i] = argv[i];
    }

    execv(gameArgv[0], gameArgv);
    perror("execv");
    free(gameArgv);
    return 1;
}
LAUNCHER

launcher_arch="arm64"
if [[ "$runtime" == "osx-x64" ]]; then
  launcher_arch="x86_64"
fi

clang -arch "$launcher_arch" "$launcher_source" -o "$launcher_path"
rm "$launcher_source"
chmod +x "$launcher_path"

cat > "$contents_dir/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>$app_name</string>
  <key>CFBundleExecutable</key>
  <string>$app_name</string>
  <key>CFBundleIdentifier</key>
  <string>$bundle_id</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$app_name</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

if [[ "$skip_sign" != "true" ]]; then
  if ! command -v codesign >/dev/null 2>&1; then
    echo "Could not find codesign. Re-run with --skip-sign to leave the bundle unsigned." >&2
    exit 1
  fi

  echo "Signing app bundle with identity '$sign_identity'..."
  codesign --force --deep --sign "$sign_identity" "$app_dir"
fi

echo "App bundle complete:"
echo "$app_dir"
