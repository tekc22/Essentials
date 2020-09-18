﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Intersystem;
using PepperDash.Core.Intersystem.Tokens;
using PepperDash.Core.WebApi.Presets;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash_Essentials_Core.Bridges.JoinMaps;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace PepperDash.Essentials.Devices.Common.VideoCodec
{
    public abstract class VideoCodecBase : ReconfigurableDevice, IRoutingInputsOutputs,
        IUsageTracking, IHasDialer, IHasContentSharing, ICodecAudio, iVideoCodecInfo, IBridgeAdvanced
    {
        private const int XSigEncoding = 28591;
        protected VideoCodecBase(DeviceConfig config)
            : base(config)
        {
            
            StandbyIsOnFeedback = new BoolFeedback(StandbyIsOnFeedbackFunc);
            PrivacyModeIsOnFeedback = new BoolFeedback(PrivacyModeIsOnFeedbackFunc);
            VolumeLevelFeedback = new IntFeedback(VolumeLevelFeedbackFunc);
            MuteFeedback = new BoolFeedback(MuteFeedbackFunc);
            SharingSourceFeedback = new StringFeedback(SharingSourceFeedbackFunc);
            SharingContentIsOnFeedback = new BoolFeedback(SharingContentIsOnFeedbackFunc);

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            ActiveCalls = new List<CodecActiveCallItem>();
        }

        public IBasicCommunication Communication { get; protected set; }

        /// <summary>
        /// An internal pseudo-source that is routable and connected to the osd input
        /// </summary>
        public DummyRoutingInputsDevice OsdSource { get; protected set; }

        public BoolFeedback StandbyIsOnFeedback { get; private set; }

        protected abstract Func<bool> PrivacyModeIsOnFeedbackFunc { get; }
        protected abstract Func<int> VolumeLevelFeedbackFunc { get; }
        protected abstract Func<bool> MuteFeedbackFunc { get; }
        protected abstract Func<bool> StandbyIsOnFeedbackFunc { get; }

        public List<CodecActiveCallItem> ActiveCalls { get; set; }

        public bool ShowSelfViewByDefault { get; protected set; }


        public bool IsReady { get; protected set; }

        public virtual List<Feedback> Feedbacks
        {
            get
            {
                return new List<Feedback>
                {
                    PrivacyModeIsOnFeedback,
                    SharingSourceFeedback
                };
            }
        }

        protected abstract Func<string> SharingSourceFeedbackFunc { get; }
        protected abstract Func<bool> SharingContentIsOnFeedbackFunc { get; }

        #region ICodecAudio Members

        public abstract void PrivacyModeOn();
        public abstract void PrivacyModeOff();
        public abstract void PrivacyModeToggle();
        public BoolFeedback PrivacyModeIsOnFeedback { get; private set; }


        public BoolFeedback MuteFeedback { get; private set; }

        public abstract void MuteOff();

        public abstract void MuteOn();

        public abstract void SetVolume(ushort level);

        public IntFeedback VolumeLevelFeedback { get; private set; }

        public abstract void MuteToggle();

        public abstract void VolumeDown(bool pressRelease);


        public abstract void VolumeUp(bool pressRelease);

        #endregion

        #region IHasContentSharing Members

        public abstract void StartSharing();
        public abstract void StopSharing();

        public bool AutoShareContentWhileInCall { get; protected set; }

        public StringFeedback SharingSourceFeedback { get; private set; }
        public BoolFeedback SharingContentIsOnFeedback { get; private set; }

        #endregion

        #region IHasDialer Members

        /// <summary>
        /// Fires when the status of any active, dialing, or incoming call changes or is new
        /// </summary>
        public event EventHandler<CodecCallStatusItemChangeEventArgs> CallStatusChange;

        /// <summary>
        /// Returns true when any call is not in state Unknown, Disconnecting, Disconnected
        /// </summary>
        public bool IsInCall
        {
            get
            {
                bool value;

                if (ActiveCalls != null)
                {
                    value = ActiveCalls.Any(c => c.IsActiveCall);
                }
                else
                {
                    value = false;
                }
                return value;
            }
        }

        public abstract void Dial(string number);
        public abstract void EndCall(CodecActiveCallItem call);
        public abstract void EndAllCalls();
        public abstract void AcceptCall(CodecActiveCallItem call);
        public abstract void RejectCall(CodecActiveCallItem call);
        public abstract void SendDtmf(string s);

        #endregion

        #region IRoutingInputsOutputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        #endregion

        #region IUsageTracking Members

        /// <summary>
        /// This object can be added by outside users of this class to provide usage tracking
        /// for various services
        /// </summary>
        public UsageTracking UsageTracker { get; set; }

        #endregion

        #region iVideoCodecInfo Members

        public VideoCodecInfo CodecInfo { get; protected set; }

        #endregion

        public event EventHandler<EventArgs> IsReadyChange;
        public abstract void Dial(Meeting meeting);

        public virtual void Dial(IInvitableContact contact)
        {
        }

        public abstract void ExecuteSwitch(object selector);

        /// <summary>
        /// Helper method to fire CallStatusChange event with old and new status
        /// </summary>
        protected void SetNewCallStatusAndFireCallStatusChange(eCodecCallStatus newStatus, CodecActiveCallItem call)
        {
            call.Status = newStatus;

            OnCallStatusChange(call);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="previousStatus"></param>
        /// <param name="newStatus"></param>
        /// <param name="item"></param>
        protected void OnCallStatusChange(CodecActiveCallItem item)
        {
            var handler = CallStatusChange;
            if (handler != null)
            {
                handler(this, new CodecCallStatusItemChangeEventArgs(item));
            }

            if (AutoShareContentWhileInCall)
            {
                StartSharing();
            }

            if (UsageTracker != null)
            {
                if (IsInCall && !UsageTracker.UsageTrackingStarted)
                {
                    UsageTracker.StartDeviceUsage();
                }
                else if (UsageTracker.UsageTrackingStarted && !IsInCall)
                {
                    UsageTracker.EndDeviceUsage();
                }
            }
        }

        /// <summary>
        /// Sets IsReady property and fires the event. Used for dependent classes to sync up their data.
        /// </summary>
        protected void SetIsReady()
        {
            IsReady = true;
            var h = IsReadyChange;
            if (h != null)
            {
                h(this, new EventArgs());
            }
        }

        // **** DEBUGGING THINGS ****
        /// <summary>
        /// 
        /// </summary>
        public virtual void ListCalls()
        {
            var sb = new StringBuilder();
            foreach (var c in ActiveCalls)
            {
                sb.AppendFormat("{0} {1} -- {2} {3}\n", c.Id, c.Number, c.Name, c.Status);
            }
            Debug.Console(1, this, "\n{0}\n", sb.ToString());
        }

        public abstract void StandbyActivate();

        public abstract void StandbyDeactivate();

        #region Implementation of IBridgeAdvanced

        public abstract void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge);

        protected void LinkVideoCodecToApi(VideoCodecBase codec, BasicTriList trilist, uint joinStart, string joinMapKey,
            EiscApiAdvanced bridge)
        {
            var joinMap = new VideoCodecControllerJoinMap(joinStart);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist {0}", trilist.ID.ToString("X"));

            LinkVideoCodecDtmfToApi(trilist, joinMap);

            LinkVideoCodecCallControlsToApi(trilist, joinMap);

            if (codec is IHasCodecCameras)
            {
                LinkVideoCodecCameraToApi(codec as IHasCodecCameras, trilist, joinMap);
            }

            if (codec is IHasCodecSelfView)
            {
                LinkVideoCodecSelfviewToApi(codec as IHasCodecSelfView, trilist, joinMap);
            }

            if (codec is IHasCameraAutoMode)
            {
                trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, true);
                LinkVideoCodecCameraModeToApi(codec as IHasCameraAutoMode, trilist, joinMap);
            }

            if (codec is IHasCodecLayouts)
            {
                LinkVideoCodecCameraLayoutsToApi(codec as IHasCodecLayouts, trilist, joinMap);
            }


        }

        private void LinkVideoCodecCallControlsToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.ManualDial.JoinNumber,
                () => Dial(trilist.StringOutput[joinMap.CurrentDialString.JoinNumber].StringValue));

            //End All calls for now
            trilist.SetSigFalseAction(joinMap.EndCall.JoinNumber, EndAllCalls);

            

            CallStatusChange += (sender, args) =>
            {
                trilist.SetBool(joinMap.HookState.JoinNumber, IsInCall);

                trilist.SetBool(joinMap.IncomingCall.JoinNumber, args.CallItem.Direction == eCodecCallDirection.Incoming);

                if (args.CallItem.Direction == eCodecCallDirection.Incoming)
                {
                    trilist.SetSigFalseAction(joinMap.IncomingAnswer.JoinNumber, () => AcceptCall(args.CallItem));
                    trilist.SetSigFalseAction(joinMap.IncomingReject.JoinNumber, () => RejectCall(args.CallItem));
                }

                var callStatusXsig = UpdateCallStatusXSig();

                trilist.SetString(joinMap.CurrentCallData.JoinNumber, callStatusXsig);
            };
        }

        private string UpdateCallStatusXSig()
        {
            const int offset = 6;
            var callIndex = 1;
            

            var tokenArray = new XSigToken[ActiveCalls.Count*offset]; //set array size for number of calls * pieces of info

            foreach (var call in ActiveCalls)
            {
                //digitals
                tokenArray[callIndex] = new XSigDigitalToken((callIndex/offset) + 1, call.IsActiveCall);

                //serials
                tokenArray[callIndex + 1] = new XSigSerialToken(callIndex, call.Name);
                tokenArray[callIndex + 2] = new XSigSerialToken(callIndex + 1, call.Number);
                tokenArray[callIndex + 3] = new XSigSerialToken(callIndex + 2, call.Direction.ToString());
                tokenArray[callIndex + 4] = new XSigSerialToken(callIndex + 3, call.Type.ToString());
                tokenArray[callIndex + 5] = new XSigSerialToken(callIndex + 4, call.Status.ToString());

                callIndex += offset;
            }

            return GetXSigString(tokenArray);
        }

        private void LinkVideoCodecDtmfToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.Dtmf0.JoinNumber, () => SendDtmf("0"));
            trilist.SetSigFalseAction(joinMap.Dtmf1.JoinNumber, () => SendDtmf("1"));
            trilist.SetSigFalseAction(joinMap.Dtmf2.JoinNumber, () => SendDtmf("2"));
            trilist.SetSigFalseAction(joinMap.Dtmf3.JoinNumber, () => SendDtmf("3"));
            trilist.SetSigFalseAction(joinMap.Dtmf4.JoinNumber, () => SendDtmf("4"));
            trilist.SetSigFalseAction(joinMap.Dtmf5.JoinNumber, () => SendDtmf("5"));
            trilist.SetSigFalseAction(joinMap.Dtmf6.JoinNumber, () => SendDtmf("6"));
            trilist.SetSigFalseAction(joinMap.Dtmf7.JoinNumber, () => SendDtmf("7"));
            trilist.SetSigFalseAction(joinMap.Dtmf8.JoinNumber, () => SendDtmf("8"));
            trilist.SetSigFalseAction(joinMap.Dtmf9.JoinNumber, () => SendDtmf("9"));
            trilist.SetSigFalseAction(joinMap.DtmfStar.JoinNumber, () => SendDtmf("*"));
            trilist.SetSigFalseAction(joinMap.DtmfPound.JoinNumber, () => SendDtmf("#"));
        }

        private void LinkVideoCodecCameraLayoutsToApi(IHasCodecLayouts codec, BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraLayout.JoinNumber, codec.LocalLayoutToggle);

            codec.LocalLayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.CameraLayoutStringFb.JoinNumber]);
        }

        private void LinkVideoCodecCameraModeToApi(IHasCameraAutoMode codec, BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraModeAuto.JoinNumber, codec.CameraAutoModeOn);
            trilist.SetSigFalseAction(joinMap.CameraModeManual.JoinNumber, codec.CameraAutoModeOff);
            
            codec.CameraAutoModeIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.CameraModeAuto.JoinNumber]);
            codec.CameraAutoModeIsOnFeedback.LinkComplementInputSig(
                trilist.BooleanInput[joinMap.CameraModeManual.JoinNumber]);
        }

        private void LinkVideoCodecSelfviewToApi(IHasCodecSelfView codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraSelfView.JoinNumber, codec.SelfViewModeToggle);

            codec.SelfviewIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.CameraSelfView.JoinNumber]);
        }

        private void LinkVideoCodecCameraToApi(IHasCodecCameras codec, BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            //Camera PTZ
            trilist.SetBoolSigAction(joinMap.CameraTiltUp.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                var camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.TiltUp();
                else camera.TiltStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraTiltDown.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                var camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.TiltDown();
                else camera.TiltStop();
            });
            trilist.SetBoolSigAction(joinMap.CameraPanLeft.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                var camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.PanLeft();
                else camera.PanStop();
            });
            trilist.SetBoolSigAction(joinMap.CameraPanRight.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                var camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.PanRight();
                else camera.PanStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraZoomIn.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                var camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.ZoomIn();
                else camera.ZoomStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraZoomOut.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                var camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.ZoomOut();
                else camera.ZoomStop();
            });

            //Camera Select
            trilist.SetUShortSigAction(joinMap.CameraNumberSelect.JoinNumber, (i) =>
            {
                if (codec.SelectedCamera == null) return;

                codec.SelectCamera(codec.Cameras[i].Key);
            });

            codec.CameraSelected += (sender, args) =>
            {
                var i = (ushort) codec.Cameras.FindIndex((c) => c.Key == args.SelectedCamera.Key);

                trilist.SetUshort(joinMap.CameraPresetSelect.JoinNumber, i);

                if (codec is IHasCodecRoomPresets)
                {
                    return;
                }

                if (!(args.SelectedCamera is IHasCameraPresets))
                {
                    return;
                }

                var cam = args.SelectedCamera as IHasCameraPresets;
                SetCameraPresetNames(cam.Presets);

                (args.SelectedCamera as IHasCameraPresets).PresetsListHasChanged += (o, eventArgs) => SetCameraPresetNames(cam.Presets);
            };

            //Camera Presets
            trilist.SetUShortSigAction(joinMap.CameraPresetSelect.JoinNumber, (i) =>
            {
                if (codec.SelectedCamera == null) return;

                var cam = codec.SelectedCamera as IHasCameraPresets;

                if (cam == null) return;

                cam.PresetSelect(i);

                trilist.SetUshort(joinMap.CameraPresetSelect.JoinNumber, i);
            });
        }

        private string SetCameraPresetNames(List<CameraPreset> presets)
        {
            var i = 1; //start index for xsig;

            var tokenArray = new XSigToken[presets.Count];

            string returnString;

            foreach (var token in presets.Select(cameraPreset => new XSigSerialToken(i, cameraPreset.Description)))
            {
                tokenArray[i - 1] = token;
                i++;
            }
            
            return GetXSigString(tokenArray);
        }

        private string GetXSigString(XSigToken[] tokenArray)
        {
            string returnString;
            using (var s = new MemoryStream())
            {
                using (var tw = new XSigTokenStreamWriter(s, true))
                {
                    tw.WriteXSigData(tokenArray);
                }

                var xSig = s.ToArray();

                returnString = Encoding.GetEncoding(XSigEncoding).GetString(xSig, 0, xSig.Length);
            }

            return returnString;
        }

        #endregion
    }


    /// <summary>
    /// Used to track the status of syncronizing the phonebook values when connecting to a codec or refreshing the phonebook info
    /// </summary>
    public class CodecPhonebookSyncState : IKeyed
    {
        private bool _InitialSyncComplete;

        public CodecPhonebookSyncState(string key)
        {
            Key = key;

            CodecDisconnected();
        }

        public bool InitialSyncComplete
        {
            get { return _InitialSyncComplete; }
            private set
            {
                if (value == true)
                {
                    var handler = InitialSyncCompleted;
                    if (handler != null)
                    {
                        handler(this, new EventArgs());
                    }
                }
                _InitialSyncComplete = value;
            }
        }

        public bool InitialPhonebookFoldersWasReceived { get; private set; }

        public bool NumberOfContactsWasReceived { get; private set; }

        public bool PhonebookRootEntriesWasRecieved { get; private set; }

        public bool PhonebookHasFolders { get; private set; }

        public int NumberOfContacts { get; private set; }

        #region IKeyed Members

        public string Key { get; private set; }

        #endregion

        public event EventHandler<EventArgs> InitialSyncCompleted;

        public void InitialPhonebookFoldersReceived()
        {
            InitialPhonebookFoldersWasReceived = true;

            CheckSyncStatus();
        }

        public void PhonebookRootEntriesReceived()
        {
            PhonebookRootEntriesWasRecieved = true;

            CheckSyncStatus();
        }

        public void SetPhonebookHasFolders(bool value)
        {
            PhonebookHasFolders = value;

            Debug.Console(1, this, "Phonebook has folders: {0}", PhonebookHasFolders);
        }

        public void SetNumberOfContacts(int contacts)
        {
            NumberOfContacts = contacts;
            NumberOfContactsWasReceived = true;

            Debug.Console(1, this, "Phonebook contains {0} contacts.", NumberOfContacts);

            CheckSyncStatus();
        }

        public void CodecDisconnected()
        {
            InitialPhonebookFoldersWasReceived = false;
            PhonebookHasFolders = false;
            NumberOfContacts = 0;
            NumberOfContactsWasReceived = false;
        }

        private void CheckSyncStatus()
        {
            if (InitialPhonebookFoldersWasReceived && NumberOfContactsWasReceived && PhonebookRootEntriesWasRecieved)
            {
                InitialSyncComplete = true;
                Debug.Console(1, this, "Initial Phonebook Sync Complete!");
            }
            else
            {
                InitialSyncComplete = false;
            }
        }
    }
}