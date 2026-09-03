        private static void DrawSaveEffect(
            Image<Rgba32> image)
        {
            DrawCircle(
                image,
                450,
                190,
                90,
                6,
                new Rgba32(
                    255,
                    80,
                    80,
                    255));

            DrawCircle(
                image,
                450,
                190,
                65,
                4,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            for (int i = 0; i < 12; i++)
            {
                double angle =
                    i * Math.PI * 2 / 12;

                int x1 =
                    450 +
                    (int)(90 * Math.Cos(angle));

                int y1 =
                    190 +
                    (int)(90 * Math.Sin(angle));

                int x2 =
                    450 +
                    (int)(125 * Math.Cos(angle));

                int y2 =
                    190 +
                    (int)(125 * Math.Sin(angle));

                DrawLine(
                    image,
                    x1,
                    y1,
                    x2,
                    y2,
                    5,
                    new Rgba32(
                        255,
                        80,
                        80,
                        255));
            }
        }

        // ============================================================
        // DRAW RECTANGLE
        // ============================================================

        private static void FillRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            Rgba32 color)
        {
            int xStart =
                Math.Max(0, x);

            int yStart =
                Math.Max(0, y);

            int xEnd =
                Math.Min(
                    image.Width,
                    x + width);

            int yEnd =
                Math.Min(
                    image.Height,
                    y + height);

            if (xStart >= xEnd ||
                yStart >= yEnd)
                return;

            image.ProcessPixelRows(
                accessor =>
                {
                    for (int yy = yStart;
                         yy < yEnd;
                         yy++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(yy);

                        for (int xx = xStart;
                             xx < xEnd;
                             xx++)
                        {
                            row[xx] = color;
                        }
                    }
                });
        }

        private static void DrawRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            int thickness,
            Rgba32 color)
        {
            FillRect(
                image,
                x,
                y,
                width,
                thickness,
                color);

            FillRect(
                image,
                x,
                y + height - thickness,
                width,
                thickness,
                color);

            FillRect(
                image,
                x,
                y,
                thickness,
                height,
                color);

            FillRect(
                image,
                x + width - thickness,
                y,
                thickness,
                height,
                color);
        }

        // ============================================================
        // DRAW LINE
        // ============================================================

        private static void DrawLine(
            Image<Rgba32> image,
            int x1,
            int y1,
            int x2,
            int y2,
            int thickness,
            Rgba32 color)
        {
            int dx =
                x2 - x1;

            int dy =
                y2 - y1;

            int steps =
                Math.Max(
                    Math.Abs(dx),
                    Math.Abs(dy));

            if (steps == 0)
            {
                FillCircle(
                    image,
                    x1,
                    y1,
                    Math.Max(
                        1,
                        thickness / 2),
                    color);

                return;
            }

            double stepX =
                dx / (double)steps;

            double stepY =
                dy / (double)steps;

            double currentX = x1;
            double currentY = y1;

            int radius =
                Math.Max(
                    1,
                    thickness / 2);

            for (int i = 0;
                 i <= steps;
                 i++)
            {
                FillCircle(
                    image,
                    (int)Math.Round(currentX),
                    (int)Math.Round(currentY),
                    radius,
                    color);

                currentX += stepX;
                currentY += stepY;
            }
        }

        // ============================================================
        // FILL CIRCLE
        // ============================================================

        private static void FillCircle(
            Image<Rgba32> image,
            int centerX,
            int centerY,
            int radius,
            Rgba32 color)
        {
            if (radius <= 0)
                return;

            int minY =
                Math.Max(
                    0,
                    centerY - radius);

            int maxY =
                Math.Min(
                    image.Height - 1,
                    centerY + radius);

            int minX =
                Math.Max(
                    0,
                    centerX - radius);

            int maxX =
                Math.Min(
                    image.Width - 1,
                    centerX + radius);

            int radiusSquared =
                radius * radius;

            image.ProcessPixelRows(
                accessor =>
                {
                    for (int y = minY;
                         y <= maxY;
                         y++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(y);

                        int dy =
                            y - centerY;

                        for (int x = minX;
                             x <= maxX;
                             x++)
                        {
                            int dx =
                                x - centerX;

                            if ((dx * dx) +
                                (dy * dy) <=
                                radiusSquared)
                            {
                                row[x] = color;
                            }
                        }
                    }
                });
        }

        // ============================================================
        // DRAW CIRCLE
        // ============================================================

        private static void DrawCircle(
            Image<Rgba32> image,
            int centerX,
            int centerY,
            int radius,
            int thickness,
            Rgba32 color)
        {
            if (radius <= 0)
                return;

            int outerRadius =
                radius;

            int innerRadius =
                Math.Max(
                    0,
                    radius - thickness);

            int minY =
                Math.Max(
                    0,
                    centerY - outerRadius);

            int maxY =
                Math.Min(
                    image.Height - 1,
                    centerY + outerRadius);

            int minX =
                Math.Max(
                    0,
                    centerX - outerRadius);

            int maxX =
                Math.Min(
                    image.Width - 1,
                    centerX + outerRadius);

            int outerSquared =
                outerRadius *
                outerRadius;

            int innerSquared =
                innerRadius *
                innerRadius;

            image.ProcessPixelRows(
                accessor =>
                {
                    for (int y = minY;
                         y <= maxY;
                         y++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(y);

                        int dy =
                            y - centerY;

                        for (int x = minX;
                             x <= maxX;
                             x++)
                        {
                            int dx =
                                x - centerX;

                            int distanceSquared =
                                (dx * dx) +
                                (dy * dy);

                            if (distanceSquared <=
                                    outerSquared &&
                                distanceSquared >=
                                    innerSquared)
                            {
                                row[x] = color;
                            }
                        }
                    }
                });
        }

        // ============================================================
        // SEND MESSAGE
        // ============================================================

        private static async Task SendMessage(
            string groupId,
            string message)
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine(
                        "MESSAGE ERROR: client is null");

                    return;
                }

                if (string.IsNullOrWhiteSpace(groupId))
                {
                    Console.WriteLine(
                        "MESSAGE ERROR: groupId is empty");

                    return;
                }

                await _client.GroupMessage(
                    groupId,
                    message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND MESSAGE ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // SEND IMAGE
        // ============================================================

        private static async Task SendImage(
            string groupId,
            byte[] imageBytes)
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine(
                        "IMAGE ERROR: client is null");

                    return;
                }

                if (string.IsNullOrWhiteSpace(groupId))
                {
                    Console.WriteLine(
                        "IMAGE ERROR: groupId is empty");

                    return;
                }

                if (imageBytes == null ||
                    imageBytes.Length == 0)
                {
                    Console.WriteLine(
                        "IMAGE ERROR: image is empty");

                    return;
                }

                Console.WriteLine(
                    "================================");

                Console.WriteLine(
                    "IMAGE TEST");

                Console.WriteLine(
                    "Group: " +
                    groupId);

                Console.WriteLine(
                    "Bytes: " +
                    imageBytes.Length);

                var result =
                    await _client.GroupMessage(
                        groupId,
                        imageBytes);

                Console.WriteLine(
                    "IMAGE SENT!");

                Console.WriteLine(
                    "Response: " +
                    result);

                Console.WriteLine(
                    "================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "IMAGE SEND ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // GET MESSAGE TEXT
        // ============================================================

        private static string GetMessageText(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "Text",
                "Message",
                "Content",
                "Body",
                "MessageText"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        // ============================================================
        // GET GROUP ID
        // ============================================================

        private static string GetGroupId(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "GroupId",
                "GroupID",
                "RoomId",
                "RoomID",
                "ChatId",
                "ChatID",
                "ConversationId",
                "ConversationID"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            // محاولة البحث داخل الخصائص المتداخلة
            string[] nestedNames =
            {
                "Group",
                "Room",
                "Chat",
                "Conversation"
            };

            foreach (string name in nestedNames)
            {
                object? nested =
                    GetObjectProperty(
                        obj,
                        name);

                if (nested == null)
                    continue;

                string value =
                    GetGroupId(nested);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        // ============================================================
        // GET USER ID
        // ============================================================

        private static string GetUserId(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "UserId",
                "UserID",
                "SenderId",
                "SenderID",
                "FromId",
                "FromID",
                "AuthorId",
                "AuthorID"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            string[] nestedNames =
            {
                "User",
                "Sender",
                "From",
                "Author"
            };

            foreach (string name in nestedNames)
            {
                object? nested =
                    GetObjectProperty(
                        obj,
                        name);

                if (nested == null)
                    continue;

                string value =
                    GetUserId(nested);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        // ============================================================
        // GET USER NAME
        // ============================================================

        private static string GetUserName(
            object obj)
        {
            if (obj == null)
                return "";

            string[] names =
            {
                "UserName",
                "Username",
                "Name",
                "SenderName",
                "DisplayName",
                "NickName",
                "Nickname"
            };

            foreach (string name in names)
            {
                string value =
                    GetStringProperty(
                        obj,
                        name);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            string[] nestedNames =
            {
                "User",
                "Sender",
                "From",
                "Author"
            };

            foreach (string name in nestedNames)
            {
                object? nested =
                    GetObjectProperty(
                        obj,
                        name);

                if (nested == null)
                    continue;

                string value =
                    GetUserName(nested);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        // ============================================================
        // REFLECTION HELPERS
        // ============================================================

        private static object? GetObjectProperty(
            object obj,
            string propertyName)
        {
            try
            {
                var property =
                    obj.GetType().GetProperty(
                        propertyName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase);

                return property?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        private static string GetStringProperty(
            object obj,
            string propertyName)
        {
            try
            {
                var property =
                    obj.GetType().GetProperty(
                        propertyName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase);

                if (property == null)
                    return "";

                object? value =
                    property.GetValue(obj);

                if (value == null)
                    return "";

                return value.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
