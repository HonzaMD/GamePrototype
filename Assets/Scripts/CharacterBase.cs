using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityTemplateProjects;

namespace Assets.Scripts
{
    [RequireComponent(typeof(Placeable))]
    public abstract class CharacterBase : MonoBehaviour
    {
        [HideInInspector]
        public SimpleCameraController Camera { get; set; }

        public abstract void GameUpdate();

        protected void AwakeB()
        {
            var p = GetComponent<Placeable>();
            p.Size = new Vector3(0.5f, 2f);
            p.Ksid = KsidEnum.Character;
            p.CellBlocking = CellBLocking.Cell1Part;
        }
    }
}
