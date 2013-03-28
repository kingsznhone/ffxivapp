// FFXIVAPP.Plugin.Parse
// EventParser.cs
//  
// Created by Ryan Wilson.
// Copyright � 2007-2013 Ryan Wilson - All Rights Reserved

#region Usings

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using FFXIVAPP.Plugin.Parse.Enums;

#endregion

namespace FFXIVAPP.Plugin.Parse.Models.Events
{
    public class EventParser
    {
        #region Property Bindings

        private SortedDictionary<UInt32, EventCode> EventCodes
        {
            get { return _eventCodes; }
        }

        #endregion

        #region Events

        public event EventHandler<Event> OnLogEvent = delegate { };
        public event EventHandler<Event> OnUnknownLogEvent = delegate { };

        #endregion

        #region Declarations

        public const UInt32 DirectionMask = 0x00003F;
        public const UInt32 SubjectMask = 0x000FC0;
        public const UInt32 TypeMask = 0x0FF000;
        public static UInt32 AllEvents = 0xFFFFFF;
        public static UInt32 UnknownEvent = 0x000000;
        private static EventParser _instance;
        private readonly SortedDictionary<UInt32, EventCode> _eventCodes = new SortedDictionary<UInt32, EventCode>();

        #endregion

        #region Initialization

        /// <summary>
        /// </summary>
        /// <param name="xml"> </param>
        private EventParser(string xml)
        {
            LoadCodes(XElement.Parse(xml));
        }

        /// <summary>
        /// </summary>
        public static EventParser Instance
        {
            get { return _instance ?? (_instance = new EventParser(Common.Constants.ChatCodesXml)); }
        }

        /// <summary>
        /// </summary>
        /// <param name="root"> </param>
        private void LoadCodes(XContainer root)
        {
            foreach (var group in root.Elements("Group"))
            {
                LoadGroups(group, new EventGroup("All"));
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="root"> </param>
        /// <param name="parent"> </param>
        private void LoadGroups(XElement root, EventGroup parent)
        {
            var thisGroup = new EventGroup((string) root.Attribute("Name"), parent);
            var type = (String) root.Attribute("Type");
            var subject = (String) root.Attribute("Subject");
            var direction = (String) root.Attribute("Direction");
            if (type != null)
            {
                switch (type)
                {
                    case "Damage":
                        thisGroup.Type = EventType.Damage;
                        break;
                    case "Failed":
                        thisGroup.Type = EventType.Failed;
                        break;
                    case "Actions":
                        thisGroup.Type = EventType.Actions;
                        break;
                    case "Items":
                        thisGroup.Type = EventType.Items;
                        break;
                    case "Cure":
                        thisGroup.Type = EventType.Cure;
                        break;
                    case "Benficial":
                        thisGroup.Type = EventType.Benficial;
                        break;
                    case "Detrimental":
                        thisGroup.Type = EventType.Detrimental;
                        break;
                    case "Chat":
                        thisGroup.Type = EventType.Chat;
                        break;
                }
            }
            if (subject != null)
            {
                switch (subject)
                {
                    case "You":
                        thisGroup.Subject = EventSubject.You;
                        break;
                    case "Party":
                        thisGroup.Subject = EventSubject.Party;
                        break;
                    case "Other":
                        thisGroup.Subject = EventSubject.Other;
                        break;
                    case "NPC":
                        thisGroup.Subject = EventSubject.NPC;
                        break;
                    case "Engaged":
                        thisGroup.Subject = EventSubject.Engaged;
                        break;
                    case "UnEngaged":
                        thisGroup.Subject = EventSubject.UnEngaged;
                        break;
                }
            }
            if (direction != null)
            {
                switch (direction)
                {
                    case "Self":
                        thisGroup.Direction = EventDirection.Self;
                        break;
                    case "Party":
                        thisGroup.Direction = EventDirection.Party;
                        break;
                    case "Other":
                        thisGroup.Direction = EventDirection.Other;
                        break;
                    case "NPC":
                        thisGroup.Direction = EventDirection.NPC;
                        break;
                    case "Engaged":
                        thisGroup.Direction = EventDirection.Engaged;
                        break;
                    case "UnEngaged":
                        thisGroup.Direction = EventDirection.UnEngaged;
                        break;
                }
            }
            foreach (var group in root.Elements("Group"))
            {
                LoadGroups(group, thisGroup);
            }
            foreach (var xElement in root.Elements("Code"))
            {
                var xKey = Convert.ToUInt32((string) xElement.Attribute("Key"), 16);
                var xDescription = (string) xElement.Element("Description");
                _eventCodes.Add(xKey, new EventCode(xDescription, xKey, thisGroup));
            }
        }

        #endregion

        #region Parsing

        /// <summary>
        /// </summary>
        /// <param name="code"> </param>
        /// <param name="line"> </param>
        /// <returns> </returns>
        private Event Parse(UInt32 code, string line)
        {
            EventCode eventCode;
            if (EventCodes.TryGetValue(code, out eventCode))
            {
                return new Event(eventCode, line);
            }
            var unknownEventCode = new EventCode
            {
                Code = code
            };
            return new Event(unknownEventCode, line);
        }

        /// <summary>
        /// </summary>
        /// <param name="code"> </param>
        /// <param name="line"> </param>
        public void ParseAndPublish(UInt32 code, string line)
        {
            var @event = Parse(code, line);
            var eventHandler = @event.IsUnknown ? OnUnknownLogEvent : OnLogEvent;
            if (eventHandler == null)
            {
                return;
            }
            lock (eventHandler)
            {
                //System.Threading.Thread.Sleep(7);
                eventHandler(this, @event);
            }
        }

        #endregion
    }
}
