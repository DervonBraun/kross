using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Реестр страниц. Хранит PageFlowDefinition, IPageView и IPageDataProvider.
    ///
    /// Регистрация происходит в Awake MonoBehaviour-компонентов или через Inspector
    /// (AutoRegisterPageView, AutoRegisterFlowDefinition).
    ///
    /// Ядро не изменяется при добавлении новых страниц.
    /// </summary>
    public sealed class PageRegistry
    {
        private readonly Dictionary<PageId, PageFlowDefinition>  _flows     = new();
        private readonly Dictionary<PageId, IPageDataProvider>   _providers = new();
        private readonly Dictionary<PageId, IPageView>           _views     = new();

        // ── Flow Definitions ──────────────────────────────────────────────────

        public void Register(PageFlowDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.PageId.IsEmpty)
            {
                Debug.LogError($"[PageRegistry] PageFlowDefinition '{def.name}' has empty PageId — skipped.");
                return;
            }

            if (_flows.ContainsKey(def.PageId))
                Debug.LogWarning($"[PageRegistry] Duplicate PageFlowDefinition for '{def.PageId}' — overwriting.");

            _flows[def.PageId] = def;
        }

        public bool TryGetFlow(PageId id, out PageFlowDefinition def) => _flows.TryGetValue(id, out def);

        // ── Views ─────────────────────────────────────────────────────────────

        public void RegisterView(IPageView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (view.ViewId.IsEmpty)
            {
                Debug.LogError("[PageRegistry] IPageView has empty ViewId — skipped.");
                return;
            }

            if (_views.ContainsKey(view.ViewId))
                Debug.LogWarning($"[PageRegistry] Duplicate IPageView for '{view.ViewId}' — overwriting.");

            _views[view.ViewId] = view;
        }

        public bool TryGetView(PageId viewId, out IPageView view) => _views.TryGetValue(viewId, out view);

        // ── Data Providers ────────────────────────────────────────────────────

        public void RegisterProvider(PageId pageId, IPageDataProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers[pageId] = provider;
        }

        public bool TryGetProvider(PageId pageId, out IPageDataProvider provider)
            => _providers.TryGetValue(pageId, out provider);

        // ── Validation helper ─────────────────────────────────────────────────

        public bool IsRegistered(PageId id) => _flows.ContainsKey(id);
    }
}
