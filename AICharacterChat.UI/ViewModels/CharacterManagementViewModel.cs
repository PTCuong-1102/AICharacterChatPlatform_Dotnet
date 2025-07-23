using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AICharacterChat.Business.Services.Interfaces;
using AICharacterChat.UI.Services;
using AICharacterChat.Data.Models;

namespace AICharacterChat.UI.ViewModels
{
    /// <summary>
    /// ViewModel for managing characters (CRUD operations)
    /// </summary>
    public partial class CharacterManagementViewModel : ViewModelBase
    {
        private readonly ICharacterService _characterService;
        private readonly ICharacterRefreshService _characterRefreshService;
        private readonly ILogger<CharacterManagementViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<CharacterViewModel> characters = new();

        [ObservableProperty]
        private CharacterViewModel? selectedCharacter;

        [ObservableProperty]
        private string characterName = string.Empty;

        [ObservableProperty]
        private string characterDescription = string.Empty;

        [ObservableProperty]
        private string systemPrompt = string.Empty;

        [ObservableProperty]
        private string avatarUrl = string.Empty;

        [ObservableProperty]
        private bool isEditing;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isSaving;

        [ObservableProperty]
        private string statusMessage = "S?n sàng";

        [ObservableProperty]
        private string searchText = string.Empty;

        public CharacterManagementViewModel(
            ICharacterService characterService,
            ICharacterRefreshService characterRefreshService,
            ILogger<CharacterManagementViewModel> logger)
        {
            _characterService = characterService;
            _characterRefreshService = characterRefreshService;
            _logger = logger;
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
        private async Task SearchCharactersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "?ang tìm ki?m...";

                var charactersData = string.IsNullOrWhiteSpace(SearchText)
                    ? await _characterService.GetAllCharactersAsync()
                    : await _characterService.SearchCharactersAsync(SearchText);

                Characters.Clear();
                foreach (var character in charactersData)
                {
                    Characters.Add(new CharacterViewModel(character));
                }

                StatusMessage = $"Tìm th?y {Characters.Count} nhân v?t";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching characters");
                StatusMessage = "L?i khi tìm ki?m nhân v?t";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectCharacter(CharacterViewModel character)
        {
            if (SelectedCharacter != null)
            {
                SelectedCharacter.IsSelected = false;
            }

            SelectedCharacter = character;
            character.IsSelected = true;

            // Load character data into form
            CharacterName = character.Name;
            CharacterDescription = character.Description ?? string.Empty;
            SystemPrompt = character.SystemPrompt;
            AvatarUrl = character.AvatarUrl ?? string.Empty;
        }

        [RelayCommand]
        private void StartNewCharacter()
        {
            // Clear any selected character
            if (SelectedCharacter != null)
            {
                SelectedCharacter.IsSelected = false;
                SelectedCharacter = null; // This will trigger property change notifications
            }

            // Ensure we're in new character mode
            IsEditing = false;

            // Clear the form
            ClearForm();

            // Update status message
            StatusMessage = "?? Hãy ?i?n thông tin nhân v?t, sau ?ó nh?n nút '? T?o nhân v?t' ho?c 'NEW CHARACTER' ?? l?u";
        }

        [RelayCommand]
        private void StartEditCharacter()
        {
            if (SelectedCharacter == null) return;

            IsEditing = true;
            StatusMessage = "Ch? ?? ch?nh s?a";
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;

            if (SelectedCharacter != null)
            {
                // Restore original values
                CharacterName = SelectedCharacter.Name;
                CharacterDescription = SelectedCharacter.Description ?? string.Empty;
                SystemPrompt = SelectedCharacter.SystemPrompt;
                AvatarUrl = SelectedCharacter.AvatarUrl ?? string.Empty;
            }
            else
            {
                ClearForm();
            }

            StatusMessage = "?ã h?y ch?nh s?a";
        }

        [RelayCommand]
        private async Task SaveCharacterAsync()
        {
            if (string.IsNullOrWhiteSpace(CharacterName) || string.IsNullOrWhiteSpace(SystemPrompt))
            {
                StatusMessage = "Vui lòng nh?p tên và system prompt";
                return;
            }

            try
            {
                IsSaving = true;
                StatusMessage = "?ang l?u nhân v?t...";

                if (IsEditing && SelectedCharacter != null)
                {
                    // Update existing character
                    var updatedCharacter = await _characterService.UpdateCharacterAsync(
                        SelectedCharacter.CharacterId,
                        CharacterName.Trim(),
                        CharacterDescription.Trim(),
                        SystemPrompt.Trim(),
                        string.IsNullOrWhiteSpace(AvatarUrl) ? null : AvatarUrl.Trim());

                    if (updatedCharacter != null)
                    {
                        SelectedCharacter.UpdateFromModel(updatedCharacter);
                        StatusMessage = "?ã c?p nh?t nhân v?t thành công";

                        // Notify other ViewModels that characters have changed
                        _characterRefreshService.NotifyCharactersChanged();
                    }
                }
                else
                {
                    // Create new character
                    var newCharacter = await _characterService.CreateCharacterAsync(
                        CharacterName.Trim(),
                        CharacterDescription.Trim(),
                        SystemPrompt.Trim(),
                        string.IsNullOrWhiteSpace(AvatarUrl) ? null : AvatarUrl.Trim());

                    var newCharacterViewModel = new CharacterViewModel(newCharacter);
                    Characters.Add(newCharacterViewModel);
                    SelectCharacter(newCharacterViewModel);

                    StatusMessage = "?ã t?o nhân v?t m?i thành công";

                    // Notify other ViewModels that characters have changed
                    _characterRefreshService.NotifyCharactersChanged();
                }

                IsEditing = false;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error saving character");
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving character");
                StatusMessage = "L?i khi l?u nhân v?t";
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private async Task DeleteCharacterAsync(CharacterViewModel character)
        {
            try
            {
                var success = await _characterService.DeleteCharacterAsync(character.CharacterId);

                if (success)
                {
                    Characters.Remove(character);

                    if (SelectedCharacter == character)
                    {
                        SelectedCharacter = null;
                        ClearForm();
                    }

                    StatusMessage = "?ã xóa nhân v?t thành công";

                    // Notify other ViewModels that characters have changed
                    _characterRefreshService.NotifyCharactersChanged();
                }
                else
                {
                    StatusMessage = "Không th? xóa nhân v?t";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting character {CharacterId}", character.CharacterId);
                StatusMessage = "L?i khi xóa nhân v?t";
            }
        }

        [RelayCommand]
        private async Task ValidateSystemPromptAsync()
        {
            if (string.IsNullOrWhiteSpace(SystemPrompt))
            {
                StatusMessage = "Vui lòng nh?p system prompt";
                return;
            }

            try
            {
                StatusMessage = "?ang ki?m tra system prompt...";

                var isValid = await _characterService.ValidateSystemPromptAsync(SystemPrompt);

                StatusMessage = isValid
                    ? "System prompt h?p l?"
                    : "System prompt có th? không ho?t ??ng t?t";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating system prompt");
                StatusMessage = "L?i khi ki?m tra system prompt";
            }
        }

        private void ClearForm()
        {
            CharacterName = string.Empty;
            CharacterDescription = string.Empty;
            SystemPrompt = string.Empty;
            AvatarUrl = string.Empty;
        }

        public bool CanSave => !string.IsNullOrWhiteSpace(CharacterName) &&
                              !string.IsNullOrWhiteSpace(SystemPrompt) &&
                              !IsSaving;

        public bool CanDelete => SelectedCharacter != null && !IsSaving;
        public bool CanEdit => SelectedCharacter != null && !IsEditing && !IsSaving;
        public bool IsNewCharacterMode => SelectedCharacter == null;

        /// <summary>
        /// Override OnPropertyChanged to notify dependent computed properties
        /// </summary>
        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Notify computed properties when their dependencies change
            switch (e.PropertyName)
            {
                case nameof(CharacterName):
                case nameof(SystemPrompt):
                case nameof(IsSaving):
                    OnPropertyChanged(nameof(CanSave));
                    break;
                case nameof(SelectedCharacter):
                    OnPropertyChanged(nameof(CanDelete));
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(IsNewCharacterMode));
                    break;
                case nameof(IsEditing):
                    OnPropertyChanged(nameof(CanEdit));
                    break;
            }
        }
    }
}
