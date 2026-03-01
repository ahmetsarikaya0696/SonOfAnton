using System;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

public class WorkspaceStateService
{
    private Guid? _selectedWorkspaceId;
    private WorkspaceDto? _selectedWorkspace;

    public event Action? OnWorkspaceChanged;

    public Guid? SelectedWorkspaceId 
    { 
        get => _selectedWorkspaceId;
        set 
        {
            if (_selectedWorkspaceId != value)
            {
                _selectedWorkspaceId = value;
                NotifyChanged();
            }
        }
    }

    public WorkspaceDto? SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            _selectedWorkspace = value;
            if (value != null && _selectedWorkspaceId != value.Id)
            {
                _selectedWorkspaceId = value.Id;
                NotifyChanged();
            }
        }
    }

    private void NotifyChanged() => OnWorkspaceChanged?.Invoke();
}
