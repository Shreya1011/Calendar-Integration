using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace GoogleCalendarAPIExample
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] Scopes = { CalendarService.Scope.Calendar, DriveService.Scope.DriveFile };
            string ApplicationName = "Calendar Integration";
            string ServiceAccountEmail = "appdevelopment@jindalsteel.com"; // Replace with the delegated user's email

            GoogleCredential credential;

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                                             .CreateScoped(Scopes)
                                             .CreateWithUser(ServiceAccountEmail);
            }

            // Create Drive API service
            var driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Ensure the "mom" folder exists and get its ID
            string folderId = GetOrCreateFolder(driveService, "mom");

            // Upload the file to the "mom" folder in Google Drive
            string fileUrl = UploadFileToDrive(driveService, "sample.pdf", folderId);

            // Create Calendar API service
            var calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Create a new event with the attachment
            string eventId = CreateCalendarEvent(calendarService, fileUrl);

            // Update the attachment of the event
            // UpdateEventAttachment(calendarService, driveService, eventId, "updated.pdf", folderId);

            // Update event details
            // UpdateCalendarEvent(calendarService, eventId, "Updated Event Summary", "Updated Description");

            // Delete the event
            // DeleteCalendarEvent(calendarService, eventId);
        }

        private static string GetOrCreateFolder(DriveService driveService, string folderName)
        {
            // Check if folder exists
            var listRequest = driveService.Files.List();
            listRequest.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}'";
            listRequest.Fields = "files(id, name)";
            var files = listRequest.Execute().Files;

            if (files != null && files.Count > 0)
            {
                return files[0].Id;
            }
            else
            {
                // Create the folder
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder"
                };
                var folder = driveService.Files.Create(fileMetadata).Execute();
                return folder.Id;
            }
        }

        private static string UploadFileToDrive(DriveService driveService, string fileName, string folderId)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };

            FilesResource.CreateMediaUpload request;
            using (var fileStream = new FileStream(fileName, FileMode.Open))
            {
                request = driveService.Files.Create(fileMetadata, fileStream, "application/pdf");
                request.Fields = "id";
                request.Upload();
            }

            var file = request.ResponseBody;

            // Share the file
            var permission = new Permission
            {
                Type = "anyone",
                Role = "reader"
            };
            driveService.Permissions.Create(permission, file.Id).Execute();

            // Get the shared file URL
            return $"https://drive.google.com/file/d/{file.Id}/view?usp=sharing";
        }

        private static string CreateCalendarEvent(CalendarService calendarService, string fileUrl)
        {
            Event newEvent = new Event()
            {
                Summary = "Google I/O 2024",
                Location = "800 Howard St., San Francisco, CA 94103",
                Description = "A chance to hear more about Google's developer products.",
                Start = new EventDateTime()
                {
                    DateTime = DateTime.Parse("2024-07-11T18:00:00+05:30"),
                    TimeZone = "Asia/Kolkata",
                },
                End = new EventDateTime()
                {
                    DateTime = DateTime.Parse("2024-07-11T19:00:00+05:30"),
                    TimeZone = "Asia/Kolkata",
                },
                Attendees = new EventAttendee[] {
                    new EventAttendee() { Email = "shreya20652@gmail.com" },
                    new EventAttendee() { Email = "sbrin@example.com" },
                },
                ConferenceData = new ConferenceData()
                {
                    CreateRequest = new CreateConferenceRequest()
                    {
                        RequestId = "sample",
                        ConferenceSolutionKey = new ConferenceSolutionKey()
                        {
                            Type = "hangoutsMeet"
                        },
                        Status = new ConferenceRequestStatus()
                        {
                            StatusCode = "success"
                        }
                    }
                },
                Attachments = new List<EventAttachment>
                {
                    new EventAttachment()
                    {
                        FileUrl = fileUrl,
                        Title = "sample"
                    }
                }
            };

            EventsResource.InsertRequest eventRequest = calendarService.Events.Insert(newEvent, "primary");
            eventRequest.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;
            eventRequest.ConferenceDataVersion = 1;
            eventRequest.SupportsAttachments = true;

            Event createdEvent = eventRequest.Execute();
            Console.WriteLine("Event created: {0}", createdEvent.HtmlLink);
            Console.WriteLine("Event ID: {0}", createdEvent.Id);

            return createdEvent.Id;
        }

        private static void UpdateEventAttachment(CalendarService calendarService, DriveService driveService, string eventId, string newFileName, string folderId)
        {
            // Upload the new file to Google Drive
            string newFileUrl = UploadFileToDrive(driveService, newFileName, folderId);

            // Get the event
            Event existingEvent = calendarService.Events.Get("primary", eventId).Execute();

            // Update the attachment
            existingEvent.Attachments = new List<EventAttachment>
            {
                new EventAttachment()
                {
                    FileUrl = newFileUrl,
                    Title = "updated_sample"
                }
            };

            // Update the event
            EventsResource.UpdateRequest updateRequest = calendarService.Events.Update(existingEvent, "primary", eventId);
            updateRequest.SendUpdates = EventsResource.UpdateRequest.SendUpdatesEnum.All;
            updateRequest.Execute();

            Console.WriteLine("Event updated with new attachment: {0}", eventId);
        }

        // private static void UpdateCalendarEvent(CalendarService calendarService, string eventId, string newSummary, string newDescription)
        // {
        //     // Get the event
        //     Event existingEvent = calendarService.Events.Get("primary", eventId).Execute();

        //     // Update the event details
        //     existingEvent.Summary = newSummary;
        //     existingEvent.Description = newDescription;

        //     // Update the event
        //     EventsResource.UpdateRequest updateRequest = calendarService.Events.Update(existingEvent, "primary", eventId);
        //     updateRequest.SendUpdates = EventsResource.UpdateRequest.SendUpdatesEnum.All;
        //     updateRequest.Execute();

        //     Console.WriteLine("Event updated: {0}", eventId);
        // }

        // private static void DeleteCalendarEvent(CalendarService calendarService, string eventId)
        // {
        //     // Delete the event
        //     EventsResource.DeleteRequest deleteRequest = calendarService.Events.Delete("primary", eventId);
        //     deleteRequest.SendUpdates = EventsResource.DeleteRequest.SendUpdatesEnum.All;
        //     deleteRequest.Execute();

        //     Console.WriteLine("Event deleted: {0}", eventId);
        // }
    }
}
