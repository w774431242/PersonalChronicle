using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Partial of <see cref="ArchiveMainTabWindow"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {

        private void GoHome()
        {
            view = MainView.Home;
            detailObjectId = null;
            overviewCategoryFilter = null;
            cachedEventDetail = null;
            ClearDetailCache();
            homeScroll = Vector2.zero;
        }

        private void GoOverview(string categoryFilter)
        {
            view = MainView.Overview;
            overviewCategoryFilter = categoryFilter;
            detailObjectId = null;
            cachedEventDetail = null;
            ClearDetailCache();
            overviewScroll = Vector2.zero;
        }

        public void RequestPawnDetail(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null)
            {
                return;
            }
            OpenPawnDetail(service, stableId);
        }

        private void OpenPawnDetail(IArchiveService service, string stableId)
        {
            OpenPawnDetail(service, stableId, 0);
        }

        /// <summary>打开殖民者档案；tabIndex=1 直接进入职业档案 Tab（v4.16 来自总览行点击）。</summary>
        private void OpenPawnDetail(IArchiveService service, string stableId, int tabIndex)
        {
            if (service == null || string.IsNullOrEmpty(stableId))
            {
                return;
            }
            detailObjectId = stableId;
            view = MainView.PawnDetail;
            detailTabIndex = tabIndex;
            cachedEventDetail = null;
            detailScroll = Vector2.zero;
            // v4.17 体检：切换详情对象必须重置社交网络图的缩放/平移/自动适配状态，
            // 否则上一个对象的 zoom/pan 泄漏到新对象（与 DrawTabBar 点击行为一致）。
            ResetSocialGraphView();
            RebuildDetailCache(service, service.GetDataRevision());
        }

        private void OpenWeaponDetail(IArchiveService service, string stableId)
        {
            if (service == null || string.IsNullOrEmpty(stableId))
            {
                return;
            }
            detailObjectId = stableId;
            view = MainView.WeaponDetail;
            detailTabIndex = 0;
            cachedEventDetail = null;
            detailScroll = Vector2.zero;
            ResetSocialGraphView();
            RebuildDetailCache(service, service.GetDataRevision());
        }

        /// <summary>重置社交网络图视图状态（缩放 1、平移 0、允许自动适配）。</summary>
        private void ResetSocialGraphView()
        {
            socialNetworkZoom = 1f;
            socialNetworkZoomTouched = false;
            socialNetworkPan = Vector2.zero;
        }

        private void OpenEventDetail(IArchiveService service, ChronicleEvent ev)
        {
            if (service == null || ev == null)
            {
                return;
            }
            cachedEventDetail = ev;
            view = MainView.EventDetail;
            detailObjectId = null;
            ClearDetailCache();
            eventScroll = Vector2.zero;
            RebuildEventCache(service, service.GetDataRevision());
        }

        private void NavigateTarget(IArchiveService service, NavTarget target, string stableId, ChronicleEvent targetEvent)
        {
            switch (target)
            {
                case NavTarget.Pawn:
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        OpenPawnDetail(service, stableId);
                    }
                    break;
                case NavTarget.Weapon:
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        OpenWeaponDetail(service, stableId);
                    }
                    break;
                case NavTarget.Event:
                    if (targetEvent != null)
                    {
                        OpenEventDetail(service, targetEvent);
                    }
                    break;
            }
        }

        private static NavTarget NavTargetOfCategory(string categoryKey)
        {
            switch (categoryKey)
            {
                case ArchiveCategoryKeys.Pawn:
                    return NavTarget.Pawn;
                case ArchiveCategoryKeys.Thing:
                    return NavTarget.Weapon;
                default:
                    return NavTarget.None;
            }
        }


    }
}
