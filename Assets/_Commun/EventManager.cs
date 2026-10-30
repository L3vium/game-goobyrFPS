using System.Collections.Generic;
using UnityEngine.Events;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.Utils
{
    //No Arguments
    public static class EventManager
    {
        private static Dictionary<EventName, UnityEvent> _eventDictionary = new Dictionary<EventName, UnityEvent>();

        public static void RegisterEvent(EventName pEventType, UnityAction pEventHandler)
        {
            if (!_eventDictionary.ContainsKey(pEventType))
            {
                _eventDictionary[pEventType] = new UnityEvent();
            }
            _eventDictionary[pEventType].AddListener(pEventHandler);
        }

        public static void UnregisterEvent(EventName pEventType, UnityAction pEventHandler)
        {
            if (_eventDictionary.ContainsKey(pEventType))
            {
                _eventDictionary[pEventType].RemoveListener(pEventHandler);
            }
        }

        public static void TriggerEvent(EventName pEventType)
        {
            if (_eventDictionary.ContainsKey(pEventType))
            {
                _eventDictionary[pEventType].Invoke();
            }
        }
    }

    //Arguments
    public static class EventManager<TEventArgs>
    {
        private static Dictionary<EventName, UnityEvent<TEventArgs>> _eventDictionary = new Dictionary<EventName, UnityEvent<TEventArgs>>();

        public static void RegisterEvent(EventName pEventType, UnityAction<TEventArgs> pEventHandler)
        {
            if (!_eventDictionary.ContainsKey(pEventType))
            {
                _eventDictionary[pEventType] = new UnityEvent<TEventArgs>();
            }

            _eventDictionary[pEventType].AddListener(pEventHandler);
        }

        public static void UnregisterEvent(EventName pEventType, UnityAction<TEventArgs> pEventHandler)
        {
            if (_eventDictionary.ContainsKey(pEventType))
            {
                _eventDictionary[pEventType].RemoveListener(pEventHandler);
            }
        }

        public static void TriggerEvent(EventName pEventType, TEventArgs pEventArgs)
        {
            if (_eventDictionary.ContainsKey(pEventType))
            {
                _eventDictionary[pEventType].Invoke(pEventArgs);
            }
        }
    }
}