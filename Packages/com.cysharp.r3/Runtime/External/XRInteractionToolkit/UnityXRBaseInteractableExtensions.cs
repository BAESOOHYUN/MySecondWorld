#if R3_XRI_SUPPORT
using UnityEngine.XR.Interaction.Toolkit;

namespace R3
{
    public static partial class UnityXRBaseInteractableExtensions
    {
        /// <summary>Observe selectEntered event.</summary>
        public static Observable<SelectEnterEventArgs> OnSelectEnteredAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.selectEntered.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe selectExited event.</summary>
        public static Observable<SelectExitEventArgs> OnSelectExitedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.selectExited.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe firstSelectEntered event.</summary>
        public static Observable<SelectEnterEventArgs> OnFirstSelectEnteredAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.firstSelectEntered.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe lastSelectExited event.</summary>
        public static Observable<SelectExitEventArgs> OnLastSelectExitedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.lastSelectExited.AsObservable(component.GetDestroyCancellationToken());
        }

        /// <summary>Observe hoverEntered event.</summary>
        public static Observable<HoverEnterEventArgs> OnHoverEnteredAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.hoverEntered.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe hoverExited event.</summary>
        public static Observable<HoverExitEventArgs> OnHoverExitedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.hoverExited.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe firstHoverEntered event.</summary>
        public static Observable<HoverEnterEventArgs> OnFirstHoverEnteredAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.firstHoverEntered.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe lastHoverExited event.</summary>
        public static Observable<HoverExitEventArgs> OnLastHoverExitedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.lastHoverExited.AsObservable(component.GetDestroyCancellationToken());
        }

        /// <summary>Observe focusEntered event.</summary>
        public static Observable<FocusEnterEventArgs> OnFocusEnteredAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.focusEntered.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe focusExited event.</summary>
        public static Observable<FocusExitEventArgs> OnFocusExitedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.focusExited.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe firstFocusEntered event.</summary>
        public static Observable<FocusEnterEventArgs> OnFirstFocusEnteredAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.firstFocusEntered.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe lastFocusExited event.</summary>
        public static Observable<FocusExitEventArgs> OnLastFocusExitedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.lastFocusExited.AsObservable(component.GetDestroyCancellationToken());
        }

        /// <summary>Observe activated event.</summary>
        public static Observable<ActivateEventArgs> OnActivatedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.activated.AsObservable(component.GetDestroyCancellationToken());
        }
        /// <summary>Observe activated event.</summary>
        public static Observable<DeactivateEventArgs> OnDeactivatedAsObservable(this UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable component)
        {
            return component.deactivated.AsObservable(component.GetDestroyCancellationToken());
        }
    }
}
#endif
