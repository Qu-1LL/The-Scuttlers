using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.UI.ViewModels;

public sealed record AssignmentEntryViewModel(int Count, IReadOnlyList<Trilobite> Creatures);
