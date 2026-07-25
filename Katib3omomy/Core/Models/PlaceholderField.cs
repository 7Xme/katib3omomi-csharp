using CommunityToolkit.Mvvm.ComponentModel;

namespace Katib3omomy.Core.Models;

public partial class PlaceholderField : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}
