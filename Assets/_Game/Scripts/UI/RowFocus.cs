using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>A settings row lights up while hovered or focused (mouse or gamepad), with an accent edge.</summary>
    public class RowFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Image _back, _edge;
        private bool _hover, _focus;
        private float _t;

        public void Init(Image pBack)
        {
            _back = pBack;
            _edge = UIFactory.Panel(transform, "Edge", UIFactory.Accent, new Vector2(0, 0.5f), new Vector2(1.5f, 0), new Vector2(3, ((RectTransform)transform).rect.height > 1f ? ((RectTransform)transform).rect.height : UIWidgets.ROW_HEIGHT));
            _edge.raycastTarget = false;
        }

        public void OnPointerEnter(PointerEventData pData) => _hover = true;
        public void OnPointerExit(PointerEventData pData) => _hover = false;
        public void OnSelect(BaseEventData pData) { _focus = true; Audio.Sfx.Play2D("hover", 0.12f); }
        public void OnDeselect(BaseEventData pData) => _focus = false;

        private void Update()
        {
            _t = Mathf.MoveTowards(_t, _hover || _focus ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (_back != null) _back.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.035f, 0.09f, _t));
            if (_edge != null) _edge.rectTransform.localScale = new Vector3(1f, _t, 1f);
        }
    }
}
