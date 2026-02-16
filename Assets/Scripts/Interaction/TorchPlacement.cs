using UnityEngine;
using OutOfPhase.Items.ToolActions;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Placement target for torches. Accepts TorchAction and spawns a placed torch prefab.
    /// </summary>
    public class TorchPlacement : MonoBehaviour, IToolTarget
    {
        [Header("Placement")]
        [SerializeField] private Transform placementPoint;
        [SerializeField] private GameObject placedTorchPrefab;
        [SerializeField] private bool disableAfterPlacement = true;

        [Header("VFX/SFX")]
        [SerializeField] private AudioClip placeSound;

        private bool _placed;

        private void Awake()
        {
            if (placementPoint == null)
                placementPoint = transform;
        }

        public bool AcceptsToolAction(System.Type actionType)
        {
            return actionType == typeof(TorchAction);
        }

        public bool ReceiveToolAction(Items.ToolAction action, Items.ToolUseContext context)
        {
            if (_placed) return false;

            var torchAction = action as TorchAction;
            if (torchAction == null) return false;

            if (placedTorchPrefab == null) return false;

            Instantiate(placedTorchPrefab, placementPoint.position, placementPoint.rotation);

            if (placeSound != null)
                AudioSource.PlayClipAtPoint(placeSound, placementPoint.position);

            _placed = true;

            if (disableAfterPlacement)
                gameObject.SetActive(false);

            return true;
        }
    }
}
