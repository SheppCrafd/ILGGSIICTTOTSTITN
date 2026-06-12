using UnityEngine;
using UnityEngine.EventSystems;

// Simple bridge component placed on each hotbar slot to forward pointer drag events to HotbarUI
public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    public HotbarUI parent;
    public int slotIndex;

    public void OnPointerDown(PointerEventData eventData)
    {
        // required so BeginDrag fires reliably on some platforms
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parent != null)
        {
            bool split = eventData.button == PointerEventData.InputButton.Right;
            bool single = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            parent.BeginDragFromSlot(slotIndex, split, single);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parent != null)
            parent.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (parent != null)
            parent.EndDrag(eventData.position);
    }
}
