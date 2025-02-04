using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Windows;

namespace WpfLocalLLM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://192.168.1.108:1234/v1"; // LM Studio默认地址
        private readonly string _modelName = "deepseek-r1-distill-qwen-14b";
        public ObservableCollection<ChatMessage> Messages { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            Messages = new ObservableCollection<ChatMessage>();
            ChatMessages.ItemsSource = Messages;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text))
                return;

            var userMessage = new ChatMessage { Role = "我(user): ", Content = MessageInput.Text };
            Messages.Add(userMessage);
            MessageInput.Clear();

            try
            {
                var requestBody = new JObject
                {
                    ["model"]= _modelName,
                    ["messages"] = new JArray
                    {
                        new JObject
                        {
                            ["role"] = "user",
                            ["content"] = userMessage.Content
                        }
                    },
                    ["temperature"] = 0.7,
                    ["max_tokens"] = 800
                };

                var content = new StringContent(
                    requestBody.ToString(Newtonsoft.Json.Formatting.None),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseBody);

                var assistantMessage = new ChatMessage
                {
                    Role = $"{_modelName}(assistant): ",
                    Content = result.Choices[0].Message.Content
                };

                Messages.Add(assistantMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class ChatCompletionResponse
    {
        [JsonProperty("choices")]
        public Choice[] Choices { get; set; }
    }

    public class Choice
    {
        [JsonProperty("message")]
        public Message Message { get; set; }
    }

    public class Message
    {
        [JsonProperty("content")]
        public string Content { get; set; }
    }


}