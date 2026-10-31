using UnityEngine;
using UnityEngine.EventSystems;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>Shows a line of explanation while a menu entry is hovered or focused (mouse or gamepad).</summary>
    public class HoverHint : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private string _hint;
        private System.Action<string> _show;

        public void Init(string pHint, System.Action<string> pShow)
        {
            _hint = pHint;
            _show = pShow;
        }

        public void OnPointerEnter(PointerEventData pData) => _show?.Invoke(_hint);
        public void OnSelect(BaseEventData pData) => _show?.Invoke(_hint);
    }
}
