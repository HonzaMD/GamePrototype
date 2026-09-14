using UnityEngine;

namespace Assets.Scripts.Utils
{
    // Nabidne v inspektoru vyber konkretniho typu pro [SerializeReference] pole (Unity 6.0 to samo
    // neumi). Vykresluje TypePickerDrawer. Az to Unity bude umet, smazat atribut i drawer.
    public class TypePickerAttribute : PropertyAttribute
    {
    }
}
