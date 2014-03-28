/*
 * Copyright (c) 2013 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except
 * in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Google.Apis.Json;
using Google.Apis.Mirror.v1;
using Google.Apis.Mirror.v1.Data;
using Google.Apis.Services;
using IdeaNotion.Mirror.ToDoList.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Mvc;

namespace IdeaNotion.Mirror.ToDoList.Controllers
{
    public class NotifyController : Controller
    {
        //
        // POST: /notify

        [HttpPost]
        public ActionResult Notify()
        {
            Notification notification =
                new NewtonsoftJsonSerializer().Deserialize<Notification>(Request.InputStream);
            String userId = notification.UserToken;
            MirrorService service = new MirrorService(new BaseClientService.Initializer()
            {
                Authenticator = Utils.GetAuthenticatorFromState(Utils.GetStoredCredentials(userId))
            });

            if (notification.Collection == "locations")
            {
                HandleLocationsNotification(notification, service);
            }
            else if (notification.Collection == "timeline")
            {
                HandleTimelineNotification(notification, service);
            }

            return new HttpStatusCodeResult((int)HttpStatusCode.OK);
        }

        /// <summary>
        /// Inserts a timeline item with the latest location information.
        /// </summary>
        /// <param name="notification">Notification payload.</param>
        /// <param name="service">Authorized Mirror service.</param>
        private void HandleLocationsNotification(Notification notification, MirrorService service)
        {

        }

        /// <summary>
        /// Process the timeline collection notification.
        /// </summary>
        /// <param name="notification">Notification payload.</param>
        /// <param name="service">Authorized Mirror service.</param>
        private void HandleTimelineNotification(Notification notification, MirrorService service)
        {
            var update = false;
            foreach (UserAction action in notification.UserActions)
            {
                if (action.Type == "DELETE")
                {
                    update = true;
                }
                else if (action.Type == "REPLY")
                {

                    var newItem = service.Timeline.Get(notification.ItemId).Fetch();
                    newItem.Text = newItem.Text;
                    newItem.BundleId = "ToDoList";
                    newItem.MenuItems = new List<MenuItem>() { 
                            { new MenuItem() { Action = "DELETE" } },
                            { new MenuItem() { Action = "READ_ALOUD" } },
                            { new MenuItem() { Action = "REPLY", Values = new List<MenuValue> { new MenuValue { DisplayName = "ADD TODO"} }  } },
                            { new MenuItem() { Action = "TOGGLE_PINNED" } },
                            };
                    service.Timeline.Update(newItem, newItem.Id).Fetch();

                    update = true;
                }
                else if (action.Type == "SHARE")
                {
                    var newItem = service.Timeline.Get(notification.ItemId).Fetch();
                    newItem.Text = newItem.Text;
                    newItem.BundleId = "ToDoList";
                    newItem.MenuItems = new List<MenuItem>() { 
                            { new MenuItem() { Action = "DELETE" } },
                            { new MenuItem() { Action = "READ_ALOUD" } },
                            { new MenuItem() { Action = "REPLY", Values = new List<MenuValue> { new MenuValue { DisplayName = "ADD TODO"} }  } },
                            { new MenuItem() { Action = "TOGGLE_PINNED" } },
                            };
                    service.Timeline.Update(newItem, newItem.Id).Fetch();

                    update = true;
                }
            }

            if (update)
            {
                MainController.UpdateToDoCover(service);
            }
        }


    }
}
