using System;
using UnityEngine;

namespace FluxForge.Mod
{
    internal sealed class ChemicalFormerSelectorLifecycle : MonoBehaviour
    {
        private PixelWindow _selector;
        private Action _destroyPreviews;

        internal void Bind(PixelWindow selector, Action destroyPreviews)
        {
            _selector = selector;
            _destroyPreviews = destroyPreviews;
        }

        private void OnDisable()
        {
            _selector.closeCallback();
            _selector.Hide();
        }

        private void OnDestroy()
        {
            _selector.closeCallback();
            _destroyPreviews();
            _selector.Destroy();
        }
    }
}
