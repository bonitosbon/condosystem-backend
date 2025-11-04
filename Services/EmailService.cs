using SendGrid;
using SendGrid.Helpers.Mail;

namespace CondoSystem.Services
{
    public interface IEmailService
    {
        Task SendBookingApprovalEmailAsync(string toEmail, string guestName, string condoName, string condoLocation, string qrCodeBase64, int bookingId, int guestCount, DateTime checkIn, DateTime checkOut, string? notes);
        Task SendBookingRejectionEmailAsync(string toEmail, string guestName, string condoName, string? rejectionReason);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendBookingApprovalEmailAsync(string toEmail, string guestName, string condoName, string condoLocation, string qrCodeBase64, int bookingId, int guestCount, DateTime checkIn, DateTime checkOut, string? notes)
        {
            try
            {
                // Try multiple ways to read the API key (environment variable with __ or : separator)
                var apiKey = _configuration["Email:SendGridApiKey"] 
                    ?? _configuration["Email__SendGridApiKey"]
                    ?? Environment.GetEnvironmentVariable("Email__SendGridApiKey");
                
                // Trim whitespace and newlines (in case it was pasted with extra spaces)
                if (!string.IsNullOrEmpty(apiKey))
                {
                    apiKey = apiKey.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
                }
                
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@regalia.com";
                var fromName = _configuration["Email:FromName"] ?? "Regalia Condo System";

                // Debug: Log all configuration values
                System.Console.WriteLine($"DEBUG: Email:SendGridApiKey = '{_configuration["Email:SendGridApiKey"]}'");
                System.Console.WriteLine($"DEBUG: Email__SendGridApiKey (env) = '{Environment.GetEnvironmentVariable("Email__SendGridApiKey")}'");
                System.Console.WriteLine($"DEBUG: Final apiKey (after trim) = '{(string.IsNullOrEmpty(apiKey) ? "NULL" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...")}'");

                if (string.IsNullOrEmpty(apiKey))
                {
                    System.Console.WriteLine("ERROR: SendGrid API key is missing. Email will not be sent.");
                    System.Console.WriteLine($"SendGridApiKey: NULL");
                    return;
                }
                
                System.Console.WriteLine($"Attempting to send approval email to: {toEmail}");
                System.Console.WriteLine($"Using SendGrid API");
                System.Console.WriteLine($"From: {fromEmail} ({fromName})");

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(fromEmail, fromName);
                var to = new EmailAddress(toEmail, guestName);
                var subject = $"Booking Approved - {condoName}";

                // Create HTML body with embedded QR code
                var htmlContent = $@"
<!DOCTYPE html>
<html class=""light"" lang=""en"">
<head>
    <meta charset=""utf-8""/>
    <meta content=""width=device-width, initial-scale=1.0"" name=""viewport""/>
    <title>Booking Confirmed - Regalia</title>
    <script src=""https://cdn.tailwindcss.com?plugins=forms,container-queries""></script>
    <link href=""https://fonts.googleapis.com"" rel=""preconnect""/>
    <link crossorigin="""" href=""https://fonts.gstatic.com"" rel=""preconnect""/>
    <link href=""https://fonts.googleapis.com/css2?family=Canva+Sans:wght@400;700&family=Roca+Two:wght@700&display=swap"" rel=""stylesheet""/>
    <style>
        .font-roca {{ font-family: 'Roca Two', serif; }}
        .font-canva {{ font-family: 'Canva Sans', sans-serif; }}
    </style>
    <script id=""tailwind-config"">
      tailwind.config = {{
        darkMode: ""class"",
        theme: {{
          extend: {{
            colors: {{
              ""primary"": ""#0599b3"",
              ""background-light"": ""#f5f8f8"",
              ""background-dark"": ""#0f2023"",
            }},
            fontFamily: {{
              ""display"": [""Inter""]
            }},
            borderRadius: {{""DEFAULT"": ""0.25rem"", ""lg"": ""0.5rem"", ""xl"": ""0.75rem"", ""full"": ""9999px""}},
          }},
        }},
      }}
    </script>
</head>
<body class=""font-canva"">
    <div class=""relative flex h-screen min-h-screen w-full flex-col items-center justify-center bg-[linear-gradient(90deg,_#0097b2,_#7ed957)] group/design-root overflow-hidden p-4"">
        <div class=""layout-container flex h-full w-full max-w-lg grow flex-col items-center justify-center"">
            <div class=""layout-content-container flex flex-col items-center justify-center rounded-xl bg-white/20 p-8 text-center text-white backdrop-blur-lg md:p-12 shadow-2xl"">
                <h1 class=""font-roca tracking-light text-4xl font-bold leading-tight pb-6"">Regalia</h1>
                
                <div class=""flex flex-col gap-2 mb-6"">
                    <p class=""font-canva text-3xl font-bold leading-tight tracking-tight md:text-4xl"">Hello, {guestName}!</p>
                    <p class=""font-canva text-xl font-semibold leading-tight opacity-95"">{condoName}</p>
                    {(string.IsNullOrWhiteSpace(condoLocation) ? "" : $@"<p class=""font-canva text-base font-normal leading-normal opacity-90"">{condoLocation}</p>")}
                </div>

                <div class=""flex flex-col gap-3 mb-6 w-full max-w-xs"">
                    <div class=""bg-white/30 rounded-lg p-4 backdrop-blur-sm"">
                        <p class=""font-canva text-sm font-semibold opacity-95 mb-1"">Check-in</p>
                        <p class=""font-canva text-lg font-bold"">{checkIn:MMM dd, yyyy 'at' h:mm tt}</p>
                    </div>
                    <div class=""bg-white/30 rounded-lg p-4 backdrop-blur-sm"">
                        <p class=""font-canva text-sm font-semibold opacity-95 mb-1"">Check-out</p>
                        <p class=""font-canva text-lg font-bold"">{checkOut:MMM dd, yyyy 'at' h:mm tt}</p>
                    </div>
                </div>

                <div class=""flex flex-col gap-2 mb-4"">
                    <p class=""font-canva text-3xl font-bold leading-tight tracking-tight md:text-4xl"">Booking Confirmed!</p>
                    <p class=""font-canva text-base font-normal leading-normal opacity-90"">Your amenity booking is complete. Please present this QR code at the front desk upon arrival.</p>
                </div>

                <div class=""mt-4 flex w-full max-w-xs grow bg-white rounded-lg p-4 shadow-lg"">
                    <div class=""w-full gap-1 overflow-hidden bg-white aspect-square rounded-lg flex"">
                        <img src=""data:image/png;base64,{qrCodeBase64}"" alt=""QR Code for booking confirmation"" class=""w-full h-full object-contain"" />
                    </div>
                </div>

                <p class=""font-canva text-sm font-normal leading-normal opacity-90 pt-6"">Scan for booking details</p>
            </div>
        </div>
    </div>
</body>
</html>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
                
                // Add QR code as attachment instead of embedding (SendGrid doesn't support cid: easily)
                // The QR code will still display in the email via base64 in the HTML
                
                System.Console.WriteLine("Sending email via SendGrid API...");
                var response = await client.SendEmailAsync(msg);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Console.WriteLine($"SUCCESS: Booking approval email sent successfully to {toEmail}");
                    System.Console.WriteLine($"SendGrid response status: {response.StatusCode}");
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    System.Console.WriteLine($"ERROR: SendGrid returned status {response.StatusCode}");
                    System.Console.WriteLine($"Response body: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"ERROR sending booking approval email: {ex.Message}");
                System.Console.WriteLine($"Error type: {ex.GetType().Name}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                // Don't throw - we don't want email failures to break the booking approval
            }
        }

        public async Task SendBookingRejectionEmailAsync(string toEmail, string guestName, string condoName, string? rejectionReason)
        {
            try
            {
                var apiKey = _configuration["Email:SendGridApiKey"];
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@regalia.com";
                var fromName = _configuration["Email:FromName"] ?? "Regalia Condo System";

                if (string.IsNullOrEmpty(apiKey))
                {
                    System.Console.WriteLine("ERROR: SendGrid API key is missing. Email will not be sent.");
                    return;
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(fromEmail, fromName);
                var to = new EmailAddress(toEmail, guestName);
                var subject = $"Booking Request - {condoName}";

                var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #e74c3c; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .info-box {{ background-color: white; padding: 15px; margin: 15px 0; border-left: 4px solid #e74c3c; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Booking Request Update</h1>
        </div>
        <div class=""content"">
            <p>Dear {guestName},</p>
            <p>Unfortunately, your booking request for <strong>{condoName}</strong> could not be approved at this time.</p>
            
            {(string.IsNullOrEmpty(rejectionReason) ? "" : $@"
            <div class=""info-box"">
                <h3>Reason:</h3>
                <p>{rejectionReason}</p>
            </div>")}

            <p>We apologize for any inconvenience. If you have any questions, please feel free to contact us.</p>
            <p>Best regards,<br>Regalia Condo System</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply.</p>
        </div>
    </div>
</body>
</html>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
                
                System.Console.WriteLine("Sending rejection email via SendGrid API...");
                var response = await client.SendEmailAsync(msg);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Console.WriteLine($"SUCCESS: Booking rejection email sent successfully to {toEmail}");
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    System.Console.WriteLine($"ERROR: SendGrid returned status {response.StatusCode}");
                    System.Console.WriteLine($"Response body: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"ERROR sending booking rejection email: {ex.Message}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't throw - we don't want email failures to break the booking rejection
            }
        }
    }
}
