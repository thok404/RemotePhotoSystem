using UdonSharp;
using UnityEngine;

namespace RemotePhotoSystem
{
    [AddComponentMenu("Remote Photo System/Remote Photo Button")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class RemotePhotoButton : UdonSharpBehaviour
    {
        public RemotePhotoGroup group;
        public RemotePhotoButtonAction buttonAction = RemotePhotoButtonAction.Random;

        [HideInInspector] public string lastTriggerError = string.Empty;

        public override void Interact()
        {
            TriggerSelectedAction();
        }

        public void TriggerSelectedAction()
        {
            lastTriggerError = string.Empty;

            if (group == null)
            {
                lastTriggerError = "Remote Photo Group is missing.";
                return;
            }

            if (buttonAction == RemotePhotoButtonAction.Random)
            {
                group.TriggerRandom();
                return;
            }

            if (buttonAction == RemotePhotoButtonAction.Previous)
            {
                TriggerPrevious();
                return;
            }

            TriggerNext();
        }

        public void TriggerRandom()
        {
            lastTriggerError = string.Empty;

            if (group == null)
            {
                lastTriggerError = "Remote Photo Group is missing.";
                return;
            }

            group.TriggerRandom();
        }

        public void TriggerPrevious()
        {
            lastTriggerError = string.Empty;

            if (group == null)
            {
                lastTriggerError = "Remote Photo Group is missing.";
                return;
            }

            group.TriggerPrevious();
        }

        public void TriggerNext()
        {
            lastTriggerError = string.Empty;

            if (group == null)
            {
                lastTriggerError = "Remote Photo Group is missing.";
                return;
            }

            group.TriggerNext();
        }
    }
}
