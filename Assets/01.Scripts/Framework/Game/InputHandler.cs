using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 입력 총관리. 키보드·마우스 이벤트를 읽어 BlockTower의 공개 API를 호출한다.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [SerializeField] BlockTower _tower;
    [SerializeField] CameraController _camera;
    [SerializeField] bool keyboardControlsEnabled = true;
    [SerializeField, Range(0.05f, 0.5f)]  float moveHoldInitialDelay  = 0.22f;
    [SerializeField, Range(0.02f, 0.25f)] float moveHoldRepeatInterval = 0.08f;

    Vector2Int _moveRepeatDir;
    float      _nextMoveRepeatTime;

    # region Life Cycle

    private void OnValidate()
    {
        if (_tower ==null) _tower = GetComponent<BlockTower>();
        if (_camera ==null) _camera = GetComponent<CameraController>();
    }

    void Awake()
    {
        if (_tower == null)  _tower  = GetComponent<BlockTower>();
        if (_camera == null) _camera = GetComponent<CameraController>();
    }

    void Update()
    {
        if (_tower == null || _tower.IsGameOver) return;

        var mouse    = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null && keyboard == null) return;

        if (_tower.IsHolding)
        {
            HandleHeldKeyboardInput(keyboard);
            if (!_tower.IsHolding) { _camera?.UpdateCameraTarget(); return; }
            _tower.UpdateHeldPosition();
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)  _tower.CancelHold();
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) _tower.CancelHold();
        }
        else
        {
            HandleSelectionKeyboardInput(keyboard);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)  _tower.HandleClick();
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) _tower.ClearSelection();
        }

        float scroll = mouse?.scroll.ReadValue().y ?? 0f;
        if (Mathf.Abs(scroll) > 0.01f)
            _camera?.ScrollCamera(scroll);

        _camera?.UpdateCameraTarget();
        _camera?.UpdateSecondaryViewCamera();
    }
    #endregion

    #region  KeyBoard Mode
    /// <summary>
    /// 키보드 선택 모드 입력 
    /// </summary>
    /// <param name="kb"></param>
    void HandleSelectionKeyboardInput(Keyboard kb)
    {
        if (!keyboardControlsEnabled || kb == null) return;
        _tower.RefreshDetachedComponents();

        bool hasMove           = ReadMoveHeld(kb, allowWasd: false, out var dir);
        bool hasConfirm        = ConfirmPressed(kb);
        bool hasCancel         = CancelPressed(kb);
        bool hasPreset         = ReadTetrominoPresetPressed(kb, out var preset);
        bool hasTab            = kb.tabKey.wasPressedThisFrame;
        bool hasPresetRotate   = ReadPresetRotationPressed(kb);
        bool hasPresetHalfTurn = ReadHalfTurnPressed(kb);

        if (!hasMove && !hasConfirm && !hasCancel && !hasPreset && !hasTab && !hasPresetRotate && !hasPresetHalfTurn) return;

        if (hasCancel)
        {
            _tower.ClearSelection();
            _tower.ClearPresetOutlinePreview();
            _tower.ClearKeyboardFocus();
            return;
        }

        _tower.EnsureFocusedCell();

        if (_tower.IsPresetSelectionActive)
        {
            _tower.HandlePresetSelectionInput(hasMove, dir, hasConfirm, hasPreset, preset, hasTab, hasPresetRotate, hasPresetHalfTurn);
            return;
        }

        if (hasPreset)
        {
            _tower.ClearSelection();
            if (_tower.HasFocusedCell) _tower.BeginPresetSelection(preset);
            return;
        }
        if (hasMove)                              _tower.MoveFocus(dir);
        if (hasConfirm)
        {
            if (_tower.SelectionCount >= 4)
                _tower.SelectionLiftBlocks();
            else if (_tower.HasFocusedCell)
                _tower.ToggleFocusedSelection();
        }
    }
    
    /// <summary>
    ///  키보드 들기 모드 입력
    /// </summary>
    /// <param name="kb"></param>
    void HandleHeldKeyboardInput(Keyboard kb)
    {
        if (!keyboardControlsEnabled || kb == null) return;

        if (ReadMoveHeld(kb, allowWasd: true, out var dir))
        {
            if (!_tower.IsUsingKeyboardPlacement) _tower.InitKeyboardPlacement();
            _tower.MoveHeldBase(dir);
        }

        if (ReadHalfTurnPressed(kb))
        {
            _tower.RotateHeldBlocks(clockwise: true);
            _tower.RotateHeldBlocks(clockwise: true);
        }
        else if (ReadHeldRotationPressed(kb, out var clockwise))
            _tower.RotateHeldBlocks(clockwise);

        if (ConfirmPressed(kb)) _tower.DropHeldToNearestSurfaceAndPlace();
        if (CancelPressed(kb))  _tower.CancelHold();
    }
    #endregion

    #region  입력 읽기 헬퍼 
    bool ReadTetrominoPresetPressed(Keyboard kb, out TetrominoPreset preset)
    {
        if (kb.qKey.wasPressedThisFrame) { preset = TetrominoPreset.I; return true; }
        if (kb.wKey.wasPressedThisFrame) { preset = TetrominoPreset.J; return true; }
        if (kb.eKey.wasPressedThisFrame) { preset = TetrominoPreset.L; return true; }
        if (kb.rKey.wasPressedThisFrame) { preset = TetrominoPreset.O; return true; }
        if (kb.aKey.wasPressedThisFrame) { preset = TetrominoPreset.S; return true; }
        if (kb.sKey.wasPressedThisFrame) { preset = TetrominoPreset.T; return true; }
        if (kb.dKey.wasPressedThisFrame) { preset = TetrominoPreset.Z; return true; }
        preset = default;
        return false;
    }
    
    
    bool ReadMoveHeld(Keyboard kb, bool allowWasd, out Vector2Int dir)
    {
        dir = CurrentMoveDirection(kb, allowWasd);
        if (dir == Vector2Int.zero)
        {
            _moveRepeatDir     = Vector2Int.zero;
            _nextMoveRepeatTime = 0f;
            return false;
        }

        bool pressedThisFrame = MoveDirectionPressedThisFrame(kb, dir, allowWasd);
        if (pressedThisFrame || dir != _moveRepeatDir)
        {
            _moveRepeatDir      = dir;
            _nextMoveRepeatTime = Time.time + moveHoldInitialDelay;
            return true;
        }
        if (Time.time < _nextMoveRepeatTime) return false;
        _nextMoveRepeatTime = Time.time + moveHoldRepeatInterval;
        return true;
    }

    bool ReadPresetRotationPressed(Keyboard kb) =>
        kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame;

    bool ReadHalfTurnPressed(Keyboard kb) =>
        kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;

    bool ReadHeldRotationPressed(Keyboard kb, out bool clockwise)
    {
        if (kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame) { clockwise = true; return true; }
        clockwise = false;
        return false;
    }

    Vector2Int CurrentMoveDirection(Keyboard kb, bool allowWasd)
    {
        if (kb.upArrowKey.isPressed)    return Vector2Int.up;
        if (kb.downArrowKey.isPressed)  return Vector2Int.down;
        if (kb.leftArrowKey.isPressed)  return Vector2Int.left;
        if (kb.rightArrowKey.isPressed) return Vector2Int.right;
        if (allowWasd)
        {
            if (kb.wKey.isPressed) return Vector2Int.up;
            if (kb.sKey.isPressed) return Vector2Int.down;
            if (kb.aKey.isPressed) return Vector2Int.left;
            if (kb.dKey.isPressed) return Vector2Int.right;
        }
        return Vector2Int.zero;
    }

    bool MoveDirectionPressedThisFrame(Keyboard kb, Vector2Int dir, bool allowWasd)
    {
        if (dir == Vector2Int.up)    return kb.upArrowKey.wasPressedThisFrame    || (allowWasd && kb.wKey.wasPressedThisFrame);
        if (dir == Vector2Int.down)  return kb.downArrowKey.wasPressedThisFrame  || (allowWasd && kb.sKey.wasPressedThisFrame);
        if (dir == Vector2Int.left)  return kb.leftArrowKey.wasPressedThisFrame  || (allowWasd && kb.aKey.wasPressedThisFrame);
        if (dir == Vector2Int.right) return kb.rightArrowKey.wasPressedThisFrame || (allowWasd && kb.dKey.wasPressedThisFrame);
        return false;
    }

    bool ConfirmPressed(Keyboard kb) =>
        kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

    bool CancelPressed(Keyboard kb) =>
        kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
    
    #endregion
}
