// Avid Project Watcher
// Copyright (C) 2026  MB+MAB
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
