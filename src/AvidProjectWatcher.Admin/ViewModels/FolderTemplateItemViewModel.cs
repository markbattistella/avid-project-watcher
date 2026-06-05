namespace AvidProjectWatcher.Admin.ViewModels;

public sealed class FolderTemplateItemViewModel(string name) : ViewModelBase
{
    private string name = name;
    private bool isEditing;

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public bool IsEditing
    {
        get => isEditing;
        set
        {
            if (SetProperty(ref isEditing, value))
            {
                RaisePropertyChanged(nameof(IsNotEditing));
            }
        }
    }

    public bool IsNotEditing => !IsEditing;
}
