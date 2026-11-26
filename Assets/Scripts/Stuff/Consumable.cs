using Assets.Scripts.Bases;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    // Jidlo: aktivace v ruce = snist. Vyzivu bere z Game.FoodTable (jidlo x konzument).
    [RequireComponent(typeof(PlaceableSibling))]
    public class Consumable : MonoBehaviour, IHoldActivate
    {
        public void Activate(Character3 character)
        {
            var label = GetComponent<PlaceableSibling>();
            if (!Game.Instance.FoodTable.TryGet(label.Ksid, character.Placeable.Ksid, out var food))
                return;

            character.Status.Eat(food.Energy, food.Waste);
            label.Kill();
        }
    }
}
