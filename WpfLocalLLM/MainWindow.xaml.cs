using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace WpfLocalLLM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
       // private readonly string _baseUrl = "http://192.168.1.108:1234/v1"; // LM Studio默认地址
       // private readonly string _modelName = "deepseek-r1-distill-qwen-14b";

        private string _selectedModel;
        private readonly Storyboard _loadingAnimation;
        public ObservableCollection<ChatMessage> Messages { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            Messages = new ObservableCollection<ChatMessage>();
            ChatMessages.ItemsSource = Messages;
            // 获取加载动画
            _loadingAnimation = (Storyboard)FindResource("LoadingAnimation");

            // 为了方便测试，直接加载模型列表
            RefreshModels_Click(null, null);
        }

        private void ShowLoading()
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            _loadingAnimation.Begin();
            SendButton.IsEnabled = false;
            MessageInput.IsEnabled = false;
        }

        private void HideLoading()
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            _loadingAnimation.Stop();
            SendButton.IsEnabled = true;
            MessageInput.IsEnabled = true;
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrlInput.Text}/models");
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("连接成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"连接失败: {response.StatusCode}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrlInput.Text}/models");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var modelsResponse = JsonConvert.DeserializeObject<ModelsResponse>(content);

                ModelSelect.ItemsSource = modelsResponse.Data;
                ModelSelect.DisplayMemberPath = "Id";
                if (modelsResponse.Data.Length > 0)
                {
                    ModelSelect.SelectedIndex = 0;
                    _selectedModel = modelsResponse.Data[0].Id;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取模型列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text))
                return;

            if (string.IsNullOrWhiteSpace(_selectedModel))
            {
                MessageBox.Show("请先选择一个模型", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userMessage = new ChatMessage { Role = "我(user): ", Content = MessageInput.Text };
            Messages.Add(userMessage);
            
            MessageInput.Clear();
            // 滚动到底部
            ChatScrollViewer.ScrollToBottom();
            ShowLoading();

            try
            {
                var requestBody = new JObject
                {
                    ["model"]= _selectedModel,
                    ["messages"] = new JArray
                    {
                        new JObject
                        {
                            ["role"] = "user",
                            ["content"] = userMessage.Content
                        }
                    },
                    ["temperature"] = 0.7,
                    ["max_tokens"] = 12000
                };

                var content = new StringContent(
                    requestBody.ToString(Newtonsoft.Json.Formatting.None),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrlInput.Text}/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseBody);

                var assistantMessage = new ChatMessage
                {
                    Role = $"{_selectedModel}(assistant): ",
                    Content = result.Choices[0].Message.Content
                };

                Messages.Add(assistantMessage);
                // 滚动到底部
                ChatScrollViewer.ScrollToBottom();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();  // 隐藏加载动画
                // 重新聚焦到输入框
                MessageInput.Focus();
            }
        }

        private void ScrollToBottomAnimation()
        {
            ChatScrollViewer.Dispatcher.InvokeAsync(() =>
            {
                var animation = new DoubleAnimation(
                    ChatScrollViewer.VerticalOffset,
                    ChatScrollViewer.ScrollableHeight,
                    TimeSpan.FromMilliseconds(200));
                ChatScrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, animation);
            });
        }

        // 需要添加一个附加属性来支持动画
        public static class ScrollViewerBehavior
        {
            public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.RegisterAttached(
                "VerticalOffset",
                typeof(double),
                typeof(ScrollViewerBehavior),
                new UIPropertyMetadata(0.0, OnVerticalOffsetChanged));

            public static void SetVerticalOffset(FrameworkElement target, double value)
            {
                target.SetValue(VerticalOffsetProperty, value);
            }

            public static double GetVerticalOffset(FrameworkElement target)
            {
                return (double)target.GetValue(VerticalOffsetProperty);
            }

            private static void OnVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
            {
                if (target is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
                }
            }
        }

        private void MessageInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

            if (e.Key == Key.Return)
            {
                e.Handled = true;  // 先阻止默认的回车行为

                // Alt+Enter 或 Shift+Enter 或 Ctrl+Enter 插入换行
                if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt) ||
                    e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ||
                    e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    int caretIndex = MessageInput.CaretIndex;
                    MessageInput.Text = MessageInput.Text.Insert(caretIndex, Environment.NewLine);
                    MessageInput.CaretIndex = caretIndex + Environment.NewLine.Length;
                    return;
                }

                // 普通回车发送消息
                SendButton_Click(null, null);
            }
        }


    }

    public class ModelsResponse
    {
        [JsonProperty("data")]
        public ModelInfo[] Data { get; set; }
    }

    public class ModelInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }
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