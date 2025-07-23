using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AICharacterChat.Business.Services.Interfaces;
using AICharacterChat.Data.Models;
using AICharacterChat.UI.Services;

namespace AICharacterChat.UI.ViewModels
{
    /// <summary>
    /// Main chat ViewModel managing the chat interface
    /// </summary>
    public partial class ChatViewModel : ViewModelBase
    {
        private readonly IChatService _chatService;
        private readonly ICharacterService _characterService;
        private readonly ICharacterRefreshService _characterRefreshService;
        private readonly ILogger<ChatViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<CharacterViewModel> characters = new();

        [ObservableProperty]
        private ObservableCollection<ConversationViewModel> conversations = new();

        [ObservableProperty]
        private ObservableCollection<MessageViewModel> messages = new();

        [ObservableProperty]
        private CharacterViewModel? selectedCharacter;

        [ObservableProperty]
        private ConversationViewModel? selectedConversation;

        [ObservableProperty]
        private string messageText = string.Empty;

        [ObservableProperty]
        private bool isSending;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusMessage = "S?n sàng";

        public ChatViewModel(
            IChatService chatService,
            ICharacterService characterService,
            ICharacterRefreshService characterRefreshService,
            ILogger<ChatViewModel> logger)
        {
            _chatService = chatService;
            _characterService = characterService;
            _characterRefreshService = characterRefreshService;
            _logger = logger;

            // Subscribe to character refresh notifications
            _characterRefreshService.CharactersChanged += OnCharactersChanged;
        }

        private async void OnCharactersChanged(object? sender, EventArgs e)
        {
            try
            {
                await LoadCharactersCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing characters after change notification");
            }
        }

        [RelayCommand]
        private async Task LoadCharactersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "?ang t?i danh sách nhân v?t...";

                var charactersData = await _characterService.GetAllCharactersAsync();

                Characters.Clear();
                foreach (var character in charactersData)
                {
                    Characters.Add(new CharacterViewModel(character));
                }

                StatusMessage = $"?ã t?i {Characters.Count} nhân v?t";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading characters");
                StatusMessage = "L?i khi t?i danh sách nhân v?t";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SelectCharacterAsync(CharacterViewModel character)
        {
            try
            {
                if (SelectedCharacter != null)
                {
                    SelectedCharacter.IsSelected = false;
                }

                SelectedCharacter = character;
                character.IsSelected = true;

                await LoadConversationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting character {CharacterId}", character.CharacterId);
                StatusMessage = "L?i khi ch?n nhân v?t";
            }
        }

        [RelayCommand]
        private async Task LoadConversationsAsync()
        {
            if (SelectedCharacter == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "?ang t?i cu?c h?i tho?i...";

                var conversationsData = await _chatService.GetConversationsAsync(SelectedCharacter.CharacterId);

                Conversations.Clear();
                foreach (var conversation in conversationsData)
                {
                    Conversations.Add(new ConversationViewModel(conversation));
                }

                StatusMessage = $"?ã t?i {Conversations.Count} cu?c h?i tho?i";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading conversations for character {CharacterId}", SelectedCharacter.CharacterId);
                StatusMessage = "L?i khi t?i cu?c h?i tho?i";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SelectConversationAsync(ConversationViewModel conversation)
        {
            try
            {
                if (SelectedConversation != null)
                {
                    SelectedConversation.IsSelected = false;
                }

                SelectedConversation = conversation;
                conversation.IsSelected = true;

                await LoadMessagesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting conversation {ConversationId}", conversation.ConversationId);
                StatusMessage = "L?i khi ch?n cu?c h?i tho?i";
            }
        }

        [RelayCommand]
        private async Task LoadMessagesAsync()
        {
            if (SelectedConversation == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "?ang t?i tin nh?n...";

                var conversationData = await _chatService.GetConversationAsync(SelectedConversation.ConversationId);

                Messages.Clear();
                if (conversationData?.Messages != null)
                {
                    foreach (var message in conversationData.Messages)
                    {
                        Messages.Add(new MessageViewModel(message));
                    }
                }

                StatusMessage = $"?ã t?i {Messages.Count} tin nh?n";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages for conversation {ConversationId}", SelectedConversation.ConversationId);
                StatusMessage = "L?i khi t?i tin nh?n";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || SelectedCharacter == null || IsSending)
                return;

            try
            {
                IsSending = true;
                StatusMessage = "?ang g?i tin nh?n...";

                var userMessage = MessageText.Trim();
                MessageText = string.Empty;

                // Add user message to UI immediately
                var userMessageViewModel = new MessageViewModel
                {
                    Sender = MessageSenders.User,
                    Content = userMessage,
                    Timestamp = DateTime.UtcNow,
                    ConversationId = SelectedConversation?.ConversationId ?? 0
                };
                Messages.Add(userMessageViewModel);

                // Send message and get AI response
                var (conversationId, aiResponse) = await _chatService.SendMessageAsync(
                    SelectedCharacter.CharacterId,
                    userMessage,
                    SelectedConversation?.ConversationId);

                // Update conversation ID if this was a new conversation
                if (SelectedConversation == null)
                {
                    await LoadConversationsAsync();
                    var newConversation = Conversations.FirstOrDefault(c => c.ConversationId == conversationId);
                    if (newConversation != null)
                    {
                        await SelectConversationAsync(newConversation);
                        return; // LoadMessagesAsync will be called by SelectConversationAsync
                    }
                }

                // Add AI response to UI
                var aiMessageViewModel = new MessageViewModel(aiResponse);
                Messages.Add(aiMessageViewModel);

                StatusMessage = "Tin nh?n ?ã ???c g?i";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                StatusMessage = "L?i khi g?i tin nh?n";
            }
            finally
            {
                IsSending = false;
            }
        }

        [RelayCommand]
        private async Task StartNewConversationAsync()
        {
            if (SelectedCharacter == null) return;

            try
            {
                if (SelectedConversation != null)
                {
                    SelectedConversation.IsSelected = false;
                }

                SelectedConversation = null;
                Messages.Clear();
                StatusMessage = "Cu?c h?i tho?i m?i ?ã s?n sàng";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new conversation");
                StatusMessage = "L?i khi t?o cu?c h?i tho?i m?i";
            }
        }

        [RelayCommand]
        private async Task DeleteConversationAsync(ConversationViewModel conversation)
        {
            try
            {
                await _chatService.DeleteConversationAsync(conversation.ConversationId);
                Conversations.Remove(conversation);

                if (SelectedConversation == conversation)
                {
                    SelectedConversation = null;
                    Messages.Clear();
                }

                StatusMessage = "?ã xóa cu?c h?i tho?i";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation {ConversationId}", conversation.ConversationId);
                StatusMessage = "L?i khi xóa cu?c h?i tho?i";
            }
        }

        public bool CanSendMessage => !string.IsNullOrWhiteSpace(MessageText) && SelectedCharacter != null && !IsSending;
    }
}

