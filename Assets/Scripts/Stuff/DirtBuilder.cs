using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    // Drzeny nastroj: mire kurzorem na bunku v dosahu a za 16 gravelu v ni postavi hlinu.
    // Misti marker (zeleny = volno, cerveny = blokovano) pres InputController.
    [RequireComponent(typeof(PlaceableSibling))]
    public class DirtBuilder : MonoBehaviour, IHandAimer, IHoldActivate, ISimpleTimerConsumer, IHasCleanup
    {
        private const int GravelCost = 12;
        private const float Reach = 1.5f;        // max vzdalenost stredu bunky od handlu (m)
        private const float Cooldown = 0.4f;   // kratka neaktivita po stavbe

        private PlaceableSibling placeable;
        private int activeTag;

        int ISimpleTimerConsumer.ActiveTag { get => activeTag; set => activeTag = value; }
        private bool OnCooldown => (activeTag & 1) != 0;

        private enum AimState { None, Free, Blocked }

        private void Awake()
        {
            placeable = GetComponent<PlaceableSibling>();
        }

        public Transform UpdateAim(Character3 character)
        {
            var toKill = ListPool<Placeable>.Rent();
            var state = Evaluate(character, toKill, out var map, out var cell);
            toKill.Return();

            var ic = Game.Instance.InputController;
            Transform marker = state switch
            {
                AimState.Free => ic.CellMarkerGreen,
                AimState.Blocked => ic.CellMarkerRed,
                _ => null,
            };
            if (marker != null)
                marker.position = map.CellToWorld(cell);
            return marker;
        }

        public void Activate(Character3 character)
        {
            var toKill = ListPool<Placeable>.Rent();
            // Defenzivne: test se dela znovu pri aktivaci, nespolehame na posledni vysledek z UpdateAim.
            var state = Evaluate(character, toKill, out var map, out var cell);
            if (state == AimState.Free && character.Inventory.TryConsume(Game.Instance.PrefabsStore.Gravel, GravelCost))
            {
                DirtFactory.BuildDirt(map, cell, placeable.LevelGroup, toKill);
                //character.ActivateHoldAnimation(placeable.Settings.ActivityAnimation, 0.55f, 2f);
                StartCooldown();
            }
            toKill.Return();
        }

        private AimState Evaluate(Character3 character, List<Placeable> toKill, out Map.Map map, out Vector2Int cell)
        {
            cell = default;
            map = placeable.GetMap();
            if (OnCooldown)
                return AimState.None;

            var ic = Game.Instance.InputController;
            Vector3 mouse = ic.GetMousePosOnZPlane(transform.position.z);
            cell = map.WorldToCell(mouse);

            Vector2 handle = placeable.GetHoldHandle().position.XY();
            if ((mouse.XY() - handle).sqrMagnitude > Reach * Reach)
                return AimState.None;

            if (character.Inventory.CountConsumable(Game.Instance.PrefabsStore.Gravel) < GravelCost)
                return AimState.None;

            return DirtFactory.CollectCellForDirt(map, cell, toKill) ? AimState.Free : AimState.Blocked;
        }

        //// Stred double-cell bunky (z = 0.25 je stred obou z-vrstev).
        //private static Vector3 MarkerPos(Map.Map map, Vector2Int cell)
        //    => (map.CellToWorld(cell) + Map.Map.CellSize2d * 0.5f).AddZ(0.25f);

        private void StartCooldown()
        {
            if (OnCooldown)
                activeTag++;
            this.Plan(Cooldown);
        }

        void ISimpleTimerConsumer.OnTimer()
        {
            if (OnCooldown)
                activeTag++;
        }

        public void Cleanup(bool goesToInventory)
        {
            // Zrusi cekajici cooldown, aby poolnuty/znovupouzity builder startoval cisty.
            if (OnCooldown)
                activeTag++;
        }
    }
}
