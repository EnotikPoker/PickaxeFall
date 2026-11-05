using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        readonly MiningGesture gesture = new();
        int gesturePointer = -1;
        Label energyLabel, abilityPrices, runBenefits;
        VisualElement energyFill, energyPanel;
        int shownEnergy = -1, shownMaximum = -1, shownSelection = -1;
        RunUpgrades shownUpgrades;
        public bool ModalOpen => tutorialOpen || modal != null && modal.style.display != DisplayStyle.None;
        void BuildCombat()
        {
            playfield.name = "gesture-field"; playfield.pickingMode = PickingMode.Position;
            playfield.RegisterCallback<PointerDownEvent>(e =>
            {
                if (gesturePointer >= 0 || e.button != 0 || !game.ControlsAvailable) return;
                gesturePointer = e.pointerId; gesture.Begin(e.position, Time.unscaledTime); playfield.CapturePointer(e.pointerId); e.StopPropagation();
            });
            playfield.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != gesturePointer) return;
                if (!game.ControlsAvailable || !playfield.worldBound.Contains(e.position)) CancelGesture(); else gesture.Move(e.position);
            });
            playfield.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != gesturePointer) return;
                bool recognized = gesture.End(e.position, Time.unscaledTime, out var kind, out var direction);
                CancelGesture();
                if (recognized && game.ControlsAvailable && game.AbilityUnlocked(kind) && playfield.worldBound.Contains(e.position))
                {
                    bool accepted = game.TryUseAbility(kind, direction);
                    if (!accepted) Toast(game.Energy.CanSpend(game.AbilityCost(kind)) ? "ДОЖДИСЬ ОКОНЧАНИЯ ПРОБИТИЯ" : "НУЖНО БОЛЬШЕ ЭНЕРГИИ");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"PICKAXEFALL_GESTURE: {kind}; accepted={accepted}; selected={game.SelectedToolIndex}; energy={game.Energy.Value}");
#endif
                }
                e.StopPropagation();
            });
            playfield.RegisterCallback<PointerCancelEvent>(_ => CancelGesture());
            playfield.RegisterCallback<PointerCaptureOutEvent>(_ => { gesture.Cancel(); gesturePointer = -1; });
            var energy = energyPanel = Box(controls, "energy-panel"); controls.Insert(0, energy);
            energyLabel = BoldText(energy, "", "energy-label"); energyLabel.name = "run-energy";
            energyFill = Box(Box(energy, "energy-track"), "energy-fill");
            abilityPrices = Text(energy, "", "ability-help");
            runBenefits = Text(resultArea, "", "benefit-summary");
            Button(resultArea, "ПОЛЬЗА УЛУЧШЕНИЙ", ShowBenefits, "stone-button benefits-button").name = "show-benefits";
        }
        public void CancelGesture()
        {
            int pointer = gesturePointer; gesturePointer = -1; gesture.Cancel();
            if (pointer >= 0 && playfield.HasPointerCapture(pointer)) playfield.ReleasePointer(pointer);
        }
        void TickCombat()
        {
            Display(energyPanel,game.AbilityUnlocked(AbilityKind.Dash)||game.AbilityUnlocked(AbilityKind.Plunge));
            if (!game.ControlsAvailable) CancelGesture();
            int value = Mathf.RoundToInt(game.Energy.Value * 10), maximum = Mathf.RoundToInt(game.Energy.Maximum * 10);
            if (shownEnergy != value || shownMaximum != maximum || shownSelection != game.SelectedToolIndex)
            {
                shownEnergy = value; shownMaximum = maximum; shownSelection = game.SelectedToolIndex;
                SetText(energyLabel, $"ЭНЕРГИЯ {Progression.Number(game.Energy.Value)} / {Progression.Number(game.Energy.Maximum)} · КИРКА {game.SelectedToolIndex + 1}");
                energyFill.style.width = Length.Percent(game.Energy.Maximum > 0 ? 100 * game.Energy.Value / game.Energy.Maximum : 0);
            }
            if (shownUpgrades != game.RunUpgrades)
            {
                shownUpgrades = game.RunUpgrades;
                string dash=game.AbilityUnlocked(AbilityKind.Dash)?Progression.Number(game.AbilityCost(AbilityKind.Dash)):"в магазине";
                string plunge=game.AbilityUnlocked(AbilityKind.Plunge)?Progression.Number(game.AbilityCost(AbilityKind.Plunge)):"в магазине";
                SetText(abilityPrices,(game.AbilityUnlocked(AbilityKind.Dash)?"Свайп: "+dash:"")+(game.AbilityUnlocked(AbilityKind.Dash)&&game.AbilityUnlocked(AbilityKind.Plunge)?" · ":"")+(game.AbilityUnlocked(AbilityKind.Plunge)?"Нажатие: "+plunge:"")+" · +"+Progression.Number(game.RunUpgrades.EnergyPerSecond)+"/с");
            }
        }
        void ShowBenefits()
        {
            if (game.Phase != RunPhase.Finished) return;
            var card = OpenModal("Польза улучшений"); Text(card, game.Benefits.Details(game.Energy.Earned), "description");
            Button(card, "ГОТОВО", () => CloseModal(), "stone-button primary");
        }
    }
}
