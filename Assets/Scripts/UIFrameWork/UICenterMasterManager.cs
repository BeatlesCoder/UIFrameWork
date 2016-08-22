﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace TinyFrameWork
{
    /// <summary>
    /// UI Main Manager "the Master" most important Class
    ///         Control all the "Big Parent" window:UIRank,UIShop,UIGame,UIMainMenu and so on.
    ///         UIRankManager: control the rank window logic (UIRankDetail sub window)
    ///         May be UIShopManager:control the UIShopDetailWindow or UIShopSubTwoWindow
    /// 
    /// 枢纽中心，控制整个大界面的显示逻辑UIRank，UIMainMenu等
    ///         UIRank排行榜界面可能也会有自己的Manager用来管理Rank系统中自己的子界面，这些子界面不交给"老大"UICenterMasterManager管理
    ///         分而治之，不是中央集权
    /// </summary>
    public class UICenterMasterManager : UIManagerBase
    {
        // save the UIRoot
        public Transform UIRoot;
        // NormalWindow node
        [System.NonSerialized]
        public Transform UINormalWindowRoot;
        // PopUpWindow node
        [System.NonSerialized]
        public Transform UIPopUpWindowRoot;
        // FixedWindow node
        [System.NonSerialized]
        public Transform UIFixedWidowRoot;

        // Each Type window start Depth
        private int fixedWindowDepth = 100;
        private int popUpWindowDepth = 150;
        private int normalWindowDepth = 2;

        // Atlas reference
        // Mask Atlas for sprite mask(Common Collider window background)
        public UIAtlas maskAtlas;

        private static UICenterMasterManager instance;
        public static UICenterMasterManager GetInstance()
        {
            return instance;
        }

        protected override void Awake()
        {
            base.Awake();
            instance = this;
            InitWindowManager();
            Debuger.Log("## UICenterMasterManager is call awake.");
        }

        public override void ShowWindow(WindowID id, ShowWindowData data = null)
        {
            UIBaseWindow baseWindow = ReadyToShowBaseWindow(id, data);
            if (baseWindow != null)
            {
                RealShowWindow(baseWindow, id);

                // 是否清空当前导航信息(回到主菜单)
                // If target window is mark as StartWindow force clear all the navigation sequence data
                if (baseWindow.windowData.isStartWindow)
                {
                    Debuger.Log("[Enter the start window, reset the backSequenceData for the navigation system.]");
                    ClearBackSequence();
                }

                if (data != null && data.forceClearBackSeqData)
                    ClearBackSequence();
            }
        }


        protected override UIBaseWindow ReadyToShowBaseWindow(WindowID id, ShowWindowData showData = null)
        {
            // Check the window control state
            if (!this.IsWindowInControl(id))
            {
                Debuger.Log("## UIManager has no control power of " + id.ToString());
                return null;
            }

            // If the window in shown list just return
            if (shownWindows.ContainsKey((int)id))
                return null;

            UIBaseWindow baseWindow = GetGameWindow(id);

            // If window not in scene start Instantiate new window to scene
            bool newAdded = false;
            if (!baseWindow)
            {
                newAdded = true;
                if (UIResourceDefine.windowPrefabPath.ContainsKey((int)id))
                {
                    string prefabPath = UIResourceDefine.UIPrefabPath + UIResourceDefine.windowPrefabPath[(int)id];
                    GameObject prefab = Resources.Load<GameObject>(prefabPath);
                    if (prefab != null)
                    {
                        GameObject uiObject = (GameObject)GameObject.Instantiate(prefab);
                        NGUITools.SetActive(uiObject, true);

                        // NOTE: You can add component to the window in the inspector
                        // Or just AddComponent<UIxxxxWindow>() to the target
                        baseWindow = uiObject.GetComponent<UIBaseWindow>();
                        // Get the window target root parent
                        Transform targetRoot = GetTargetRoot(baseWindow.windowData.windowType);
                        GameUtility.AddChildToTarget(targetRoot, baseWindow.gameObject.transform);
                        allWindows[(int)id] = baseWindow;
                    }
                }
            }

            if (baseWindow == null)
                Debug.LogError("[window instance is null.]" + id.ToString());

            // Call reset window when first load new window
            // Or get forceResetWindow param
            if (newAdded || (showData != null && showData.forceResetWindow))
                baseWindow.ResetWindow();

            // refresh the navigation data
            RefreshBackSequenceData(baseWindow);

            // Adjust the window depth
            AdjustBaseWindowDepth(baseWindow);

            // Add common background collider to window
            AddColliderBgForWindow(baseWindow);
            return baseWindow;
        }

        public override void HideWindow(WindowID id, Action onComplete = null)
        {
            CheckDirectlyHide(id, onComplete);
        }

        public override void InitWindowManager()
        {
            base.InitWindowManager();
            InitWindowControl();
            isNeedWaitHideOver = true;

            DontDestroyOnLoad(UIRoot);

            if (UIFixedWidowRoot == null)
            {
                UIFixedWidowRoot = new GameObject("UIFixedWidowRoot").transform;
                GameUtility.AddChildToTarget(UIRoot, UIFixedWidowRoot);
                GameUtility.ChangeChildLayer(UIFixedWidowRoot, UIRoot.gameObject.layer);
            }
            if (UIPopUpWindowRoot == null)
            {
                UIPopUpWindowRoot = new GameObject("UIPopUpWindowRoot").transform;
                GameUtility.AddChildToTarget(UIRoot, UIPopUpWindowRoot);
                GameUtility.ChangeChildLayer(UIPopUpWindowRoot, UIRoot.gameObject.layer);
            }
            if (UINormalWindowRoot == null)
            {
                UINormalWindowRoot = new GameObject("UINormalWindowRoot").transform;
                GameUtility.AddChildToTarget(UIRoot, UINormalWindowRoot);
                GameUtility.ChangeChildLayer(UINormalWindowRoot, UIRoot.gameObject.layer);
            }
        }

        protected override void InitWindowControl()
        {
            managedWindowIds.Clear();
            AddWindowInControl(WindowID.WindowID_Level);
            AddWindowInControl(WindowID.WindowID_Rank);
            AddWindowInControl(WindowID.WindowID_MainMenu);
            AddWindowInControl(WindowID.WindowID_TopBar);
            AddWindowInControl(WindowID.WindowID_MessageBox);
            AddWindowInControl(WindowID.WindowID_LevelDetail);
            AddWindowInControl(WindowID.WindowID_Matching);
            AddWindowInControl(WindowID.WindowID_MatchResult);
            AddWindowInControl(WindowID.WindowID_Skill);
        }

        public override void ClearAllWindow()
        {
            base.ClearAllWindow();
        }

        /// <summary>
        /// Return logic 
        /// When return back navigation check current window's Return Logic
        /// If true just execute the return logic
        /// If false just immediately enter the RealReturnWindow() logic
        /// </summary>
        public override bool ReturnWindow()
        {
            if (curShownNormalWindow != null)
            {
                bool needReturn = curShownNormalWindow.ExecuteReturnLogic();
                if (needReturn)
                    return false;
            }
            return RealReturnWindow();
        }

        /// <summary>
        /// Calculate right depth with windowType
        /// </summary>
        /// <param name="baseWindow"></param>
        private void AdjustBaseWindowDepth(UIBaseWindow baseWindow)
        {
            UIWindowType windowType = baseWindow.windowData.windowType;
            int needDepth = 1;
            if (windowType == UIWindowType.Normal)
            {
                needDepth = Mathf.Clamp(GameUtility.GetMaxTargetDepth(UINormalWindowRoot.gameObject, false) + 1, normalWindowDepth, int.MaxValue);
                Debuger.Log("[UIWindowType.Normal] maxDepth is " + needDepth + baseWindow.GetID);
            }
            else if (windowType == UIWindowType.PopUp)
            {
                needDepth = Mathf.Clamp(GameUtility.GetMaxTargetDepth(UIPopUpWindowRoot.gameObject) + 1, popUpWindowDepth, int.MaxValue);
                Debuger.Log("[UIWindowType.PopUp] maxDepth is " + needDepth);
            }
            else if (windowType == UIWindowType.Fixed)
            {
                needDepth = Mathf.Clamp(GameUtility.GetMaxTargetDepth(UIFixedWidowRoot.gameObject) + 1, fixedWindowDepth, int.MaxValue);
                Debuger.Log("[UIWindowType.Fixed] max depth is " + needDepth);
            }
            if (baseWindow.MinDepth != needDepth)
                GameUtility.SetTargetMinPanel(baseWindow.gameObject, needDepth);
            baseWindow.MinDepth = needDepth;
        }

        /// <summary>
        /// Add Collider and BgTexture for target window
        /// </summary>
        private void AddColliderBgForWindow(UIBaseWindow baseWindow)
        {
            UIWindowColliderMode colliderMode = baseWindow.windowData.colliderMode;
            if (colliderMode == UIWindowColliderMode.None)
                return;
            if (colliderMode == UIWindowColliderMode.Normal)
                GameUtility.AddColliderBgToTarget(baseWindow.gameObject, "Mask02", maskAtlas, true);
            if (colliderMode == UIWindowColliderMode.WithBg)
                GameUtility.AddColliderBgToTarget(baseWindow.gameObject, "Mask02", maskAtlas, false);
        }

        public void RefreshBackSequenceData(UIBaseWindow baseWindow)
        {
            WindowData windowData = baseWindow.windowData;
            if (baseWindow.RefreshBackSeqData)
            {
                bool dealBackSequence = true;
                if (curShownNormalWindow != null)
                {
                    if (curShownNormalWindow.windowData.showMode == UIWindowShowMode.NoNeedBack)
                    {
                        dealBackSequence = false;
                        HideWindow(curShownNormalWindow.GetID, null);
                    }
                    Debuger.Log("## UICenterMasterManager : current shown Normal Window is " + curShownNormalWindow.GetID);
                }

                if (shownWindows.Count > 0 && dealBackSequence)
                {
                    List<WindowID> removedKey = null;
                    List<WindowID> newPushList = new List<WindowID>();
                    List<UIBaseWindow> sortByMinDepth = new List<UIBaseWindow>();

                    BackWindowSequenceData backData = new BackWindowSequenceData();

                    foreach (KeyValuePair<int, UIBaseWindow> window in shownWindows)
                    {
                        bool needToHide = true;
                        if (windowData.showMode == UIWindowShowMode.NeedBack
                            || window.Value.windowData.windowType == UIWindowType.Fixed)
                            needToHide = false;

                        if (needToHide)
                        {
                            if (removedKey == null)
                                removedKey = new List<WindowID>();
                            removedKey.Add((WindowID)window.Key);

                            // HideOther Type(hide all window directly)
                            window.Value.HideWindowDirectly();
                        }

                        // 将Window添加到BackSequence中
                        if (window.Value.CanAddedToBackSeq)
                            sortByMinDepth.Add(window.Value);
                    }

                    if (removedKey != null)
                    {
                        for (int i = 0; i < removedKey.Count; i++)
                            shownWindows.Remove((int)removedKey[i]);
                    }

                    // push to backToShowWindows stack
                    if (sortByMinDepth.Count > 0)
                    {
                        // 按照层级顺序存入显示List中 
                        // Add to return show target list
                        sortByMinDepth.Sort(new CompareBaseWindow());
                        for (int i = 0; i < sortByMinDepth.Count; i++)
                        {
                            WindowID pushWindowId = sortByMinDepth[i].GetID;
                            newPushList.Add(pushWindowId);
                        }

                        backData.hideTargetWindow = baseWindow;
                        backData.backShowTargets = newPushList;
                        backSequence.Push(backData);
                    }
                }
            }
            else if (windowData.showMode == UIWindowShowMode.NoNeedBack)
                HideAllShownWindow(true);

            CheckBackSequenceData(baseWindow);
        }

        private void CheckBackSequenceData(UIBaseWindow baseWindow)
        {
            // 如果当前存在BackSequence数据
            // 1.栈顶界面不是当前要显示的界面需要清空BackSequence(导航被重置)
            // 2.栈顶界面是当前显示界面,如果类型为(NeedBack)则需要显示所有backShowTargets界面
            WindowData windowData = baseWindow.windowData;
            if (baseWindow.RefreshBackSeqData)
            {
                if (backSequence.Count > 0)
                {
                    BackWindowSequenceData backData = backSequence.Peek();
                    if (backData.hideTargetWindow != null)
                    {
                        // 栈顶不是即将显示界面(导航序列被打断)
                        // 如果当前导航队列顶部元素和当前显示的界面一致，表示和当前的导航数衔接上，后续导航直接使用导航数据
                        // 不一致则表示，导航已经失效，下次点击返回按钮，我们直接根据window的preWindowId确定跳转到哪一个界面

                        // 如果测试：进入到demo的 关卡详情，点击失败按钮，然后你可以选择从游戏中跳转到哪一个界面，查看导航输出信息
                        // 可以知道是否破坏了导航数据

                        // if the navigation stack top window not equals to current show window just clear the navigation stack
                        // check whether the navigation is broken

                        // Example:(we from mainmenu to uilevelwindow to uileveldetailwindow)
                        // UILevelDetailWindow <- UILevelWindow <- UIMainMenu   (current navigation stack top element is UILevelDetailWindow)

                        // click the GotoGame in UILevelDetailWindow to enter the real Game

                        // 1. Exit game we want to enter UILevelDetailWindow(OK, the same as navigation stack top UILevelDetailWindow) so we not break the navigation
                        // when we enter the UILevelDetailWindow our system will follow the navigation system

                        // 2. Exit game we want to enter UISkillWindow(OK, not the same as navigation stack top UILevelDetailWindow)so we break the navigation
                        // reset the navigation data 
                        // when we click return Button in the UISkillWindow we will find UISkillWindow's preWindowId to navigation because our navigation data is empty
                        // we should use preWindowId for navigating to next window

                        // HOW to Test
                        // when you in the MatchResultWindow , you need click the lose button choose to different window and check the ConsoleLog find something useful

                        if (backData.hideTargetWindow.GetID != baseWindow.GetID)
                        {
                            Debuger.Log("## UICenterMasterManager : Need to clear all back window sequence data ##");
                            Debuger.Log("## UICenterMasterManager : Hide target window and show window id is " + backData.hideTargetWindow.GetID + " != " + baseWindow.GetID);
                            backSequence.Clear();
                        }
                        else
                        {
                            // NeedBack类型要将backShowTargets界面显示
                            if (windowData.showMode == UIWindowShowMode.NeedBack
                                && backData.backShowTargets != null)
                            {
                                for (int i = 0; i < backData.backShowTargets.Count; i++)
                                {
                                    WindowID backId = backData.backShowTargets[i];
                                    // 保证最上面为currentShownWindow
                                    if (i == backData.backShowTargets.Count - 1)
                                    {
                                        Debug.Log("change currentShownNormalWindow : " + backId);
                                        // 改变当前活跃Normal窗口
                                        this.lastShownNormalWindow = this.curShownNormalWindow;
                                        this.curShownNormalWindow = GetGameWindow(backId);
                                    }
                                    ShowWindowForNavigation(backId);
                                }
                            }
                        }
                    }
                    else
                        Debuger.LogError("Back data hide target window is null!");
                }
            }
        }

        public Transform GetTargetRoot(UIWindowType type)
        {
            if (type == UIWindowType.Fixed)
                return UIFixedWidowRoot;
            if (type == UIWindowType.Normal)
                return UINormalWindowRoot;
            if (type == UIWindowType.PopUp)
                return UIPopUpWindowRoot;
            return UIRoot;
        }

        /// <summary>
        /// MessageBox
        /// </summary>
        /// 
        public void ShowMessageBox(string msg)
        {
            UIBaseWindow msgWindow = ReadyToShowBaseWindow(WindowID.WindowID_MessageBox);
            if (msgWindow != null)
            {
                ((UIMessageBox)msgWindow).SetMsg(msg);
                ((UIMessageBox)msgWindow).ResetWindow();
                RealShowWindow(msgWindow, WindowID.WindowID_MessageBox);
            }
        }

        public void ShowMessageBox(string msg, string centerStr, UIEventListener.VoidDelegate callBack)
        {
            UIBaseWindow msgWindow = ReadyToShowBaseWindow(WindowID.WindowID_MessageBox);
            if (msgWindow != null)
            {
                UIMessageBox messageBoxWindow = ((UIMessageBox)msgWindow);
                ((UIMessageBox)msgWindow).ResetWindow();
                messageBoxWindow.SetMsg(msg);
                messageBoxWindow.SetCenterBtnCallBack(centerStr, callBack);
                RealShowWindow(msgWindow, WindowID.WindowID_MessageBox);
            }
        }

        public void ShowMessageBox(string msg, string leftStr, UIEventListener.VoidDelegate leftCallBack, string rightStr, UIEventListener.VoidDelegate rightCallBack)
        {
            UIBaseWindow msgWindow = ReadyToShowBaseWindow(WindowID.WindowID_MessageBox);
            if (msgWindow != null)
            {
                UIMessageBox messageBoxWindow = ((UIMessageBox)msgWindow);
                ((UIMessageBox)msgWindow).ResetWindow();
                messageBoxWindow.SetMsg(msg);
                messageBoxWindow.SetRightBtnCallBack(rightStr, rightCallBack);
                messageBoxWindow.SetLeftBtnCallBack(leftStr, leftCallBack);
                RealShowWindow(msgWindow, WindowID.WindowID_MessageBox);
            }
        }

        public void CloseMessageBox(Action onClosed = null)
        {
            HideWindow(WindowID.WindowID_MessageBox);
        }
    }
}