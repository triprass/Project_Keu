using System.Net.Http.Headers;
using System.Text.Json;


namespace Project_Keu.Services.Notifications
{
    public class FonnteResponse
    {
        public bool Status { get; set; }
        public string Detail { get; set; }
    }

    public interface IFonnteService
    {
        Task<bool> SendMessageAsync(string target, string message);
    }

    public class FonnteService : IFonnteService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public FonnteService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> SendMessageAsync(string target, string message)
        {
            var apiUrl = _configuration["Fonnte:ApiUrl"];
            var token = _configuration["Fonnte:Token"];

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);

            // Authorization header menggunakan token Fonnte
            request.Headers.Add("Authorization", token);

            // Fonnte menerima format Form-Data (MultipartContent)
            var formData = new MultipartFormDataContent
        {
            { new StringContent(target), "target" },
            { new StringContent(message), "message" }
        };

            request.Content = formData;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var jsonString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FonnteResponse>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Status ?? false;
        }
    }
}
