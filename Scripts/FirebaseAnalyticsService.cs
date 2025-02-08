using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Messaging;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using Larje.Core;
using Larje.Core.Services;
using UnityEngine;

namespace Larje.Analytics.Firebase
{
    [BindService(typeof(IAnalyticsService), typeof(FirebaseAnalyticsService))]
    public class FirebaseAnalyticsService : Service, IAnalyticsService
    {
        [SerializeField] private bool useRemoteConfig;
        [SerializeField] private bool useCloudMessaging;
        [Header("Analytics Keys")]
        [SerializeField] private string stringKey = "larje_key_string";
        [SerializeField] private string intKey = "larje_key_num";
        [SerializeField] private string jsonKey = "larje_key_json";
        [SerializeField] private string boolKey = "larje_key_bool";

        [InjectService] private DataService _dataService;
        
        private DependencyStatus _dependencyStatus = DependencyStatus.UnavailableOther;
        
        public override void Init()
        {
            
        }

        public void SendEvent(string eventName)
        {
            FirebaseAnalytics.LogEvent(eventName);
            Debug.Log($"<color=yellow>{eventName}</color>");
        }

        public void FetchFirebase()
        {
            if (!useRemoteConfig) return;

            FetchDataAsync();
        }

        public void DisplayData() 
        {
            Debug.Log("Current Data:");
            Debug.Log($"{stringKey}: " + FirebaseRemoteConfig.DefaultInstance.GetValue(stringKey).StringValue);
            Debug.Log($"{intKey}: " + FirebaseRemoteConfig.DefaultInstance.GetValue(intKey).LongValue);
            Debug.Log($"{jsonKey}: " + FirebaseRemoteConfig.DefaultInstance.GetValue(jsonKey).StringValue);
            Debug.Log($"{boolKey}: " + FirebaseRemoteConfig.DefaultInstance.GetValue(boolKey).BooleanValue);
        }
        
        public void DisplayAllKeys() 
        {
            Debug.Log("Current Keys:");
            
            System.Collections.Generic.IEnumerable<string> keys = FirebaseRemoteConfig.DefaultInstance.Keys;
            
            foreach (string key in keys) 
            {
                Debug.Log(key);
            }
        }
        
        public void OnTokenReceived(object sender, TokenReceivedEventArgs token)
        {
            Debug.Log("Received Registration Token: " + token.Token);
        }

        public void OnMessageReceived(object sender, MessageReceivedEventArgs message)
        {
            Debug.Log("Received a new message from: " + message.Message.From);
        }

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        private void Start()
        {
            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                _dependencyStatus = task.Result;
                if (_dependencyStatus == DependencyStatus.Available)
                {
                    OnFirebaseInitialized();
                    if(useCloudMessaging)
                    {
                        FirebaseMessaging.TokenReceived += OnTokenReceived;
                        FirebaseMessaging.MessageReceived += OnMessageReceived;
                    }
                }
                else
                {
                    Debug.LogError(
                        "Could not resolve all Firebase dependencies: " + _dependencyStatus);
                }
            });
        }

        private void OnFirebaseInitialized()
        {
            Debug.Log("FIREBASE INITIALIZED");
            SendEvent($"Start_Session_{_dataService.Data.IternalData.SessionNum}");

            InitializeDefaultInstance();
        }

        private void InitializeDefaultInstance()
        {
            if (!useRemoteConfig) return;
            
            System.Collections.Generic.Dictionary<string, object> defaults =
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { stringKey, "default local string" },
                    { intKey, 1 },
                    { jsonKey, 1.0 },
                    { boolKey, false }
                };

            FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults)
                .ContinueWithOnMainThread(task =>
                {
                    Debug.Log("RemoteConfig configured and ready!");
                });
        }

        private Task FetchDataAsync()
        {
            Debug.Log("Fetching data...");
            Task fetchTask =
                FirebaseRemoteConfig.DefaultInstance.FetchAsync(
                    TimeSpan.Zero);
            return fetchTask.ContinueWithOnMainThread(FetchComplete);
        }

        private void FetchComplete(Task fetchTask)
        {
            if (fetchTask.IsCanceled)
            {
                Debug.Log("Fetch canceled.");
            }
            else if (fetchTask.IsFaulted)
            {
                Debug.Log("Fetch encountered an error.");
            }
            else if (fetchTask.IsCompleted)
            {
                Debug.Log("Fetch completed successfully!");
            }

            ConfigInfo info = FirebaseRemoteConfig.DefaultInstance.Info;
            
            switch (info.LastFetchStatus)
            {
                case LastFetchStatus.Success:
                    FirebaseRemoteConfig.DefaultInstance.ActivateAsync()
                        .ContinueWithOnMainThread(task =>
                        {
                            Debug.Log($"Remote data loaded and ready (last fetch time {info.FetchTime}).");
                        });

                    break;
                case LastFetchStatus.Failure:
                    switch (info.LastFetchFailureReason)
                    {
                        case FetchFailureReason.Error:
                            Debug.Log("Fetch failed for unknown reason");
                            break;
                        case FetchFailureReason.Throttled:
                            Debug.Log("Fetch throttled until " + info.ThrottledEndTime);
                            break;
                    }

                    break;
                case LastFetchStatus.Pending:
                    Debug.Log("Latest Fetch call still pending.");
                    break;
            }
        }
    }
}