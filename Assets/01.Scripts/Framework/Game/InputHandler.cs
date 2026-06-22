using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [SerializeField] BlockTower _tower;
    [SerializeField] CameraController _camera;
    [SerializeField] bool keyboardControlsEnabled = true;

    TowerPhysicsController       _physicsController;
    HeldBlockController          _held;
    TetrominoSelectionController _selection;
    BlockExtractionController    _extraction;
    ScoreController              _scoreController;
    PlacementZoneController      _placement;
    HeldPlacementController      _heldPlacement;

    [SerializeField, Range(0.05f, 0.5f)]  float moveHoldInitialDelay   = 0.22f;
    [SerializeField, Range(0.02f, 0.25f)] float moveHoldRepeatInterval = 0.08f;
    [SerializeField, Range(0f, 0.5f)] float placementConfirmDelay = 0.2f;

    Vector2Int _moveRepeatDir;
    float      _nextMoveRepeatTime;
    bool       _holdStartedByMouse;
    bool       _wasHolding;
    float      _placementConfirmAllowedAt;

    #region Lifecycle

    void OnValidate()
    {
        if (_tower  == null) _tower  = GetComponent<BlockTower>();
        if (_camera == null) _camera = GetComponent<CameraController>();
    }

    void Awake()
    {
        if (_tower            == null) _tower            = GetComponent<BlockTower>();
        if (_camera           == null) _camera           = GetComponent<CameraController>();
        _physicsController = GetComponent<TowerPhysicsController>();
        _held              = GetComponent<HeldBlockController>();
        _selection         = GetComponent<TetrominoSelectionController>();
        _extraction        = GetComponent<BlockExtractionController>();
        _scoreController   = GetComponent<ScoreController>();
        _placement         = GetComponent<PlacementZoneController>();
        _heldPlacement     = GetComponent<HeldPlacementController>();
    }

    void Update()
    {
        if (_tower == null || (_scoreController != null && _scoreController.IsGameOver)) return;
        RefreshHoldingTransition();
        if (_tower.IsResolvingTopPuyo)
        {
            _camera?.UpdateCameraTarget();
            _camera?.UpdateSecondaryViewCamera();
            return;
        }

        var mouse    = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null && keyboard == null) return;

        if (_held != null && _held.IsHolding)
        {
            HandleHeldKeyboardInput(keyboard);
            if (!_held.IsHolding) { _camera?.UpdateCameraTarget(); return; }

            if (_holdStartedByMouse && !_held.UsingKeyboardPlacement)
            {
                _heldPlacement?.UpdateHeldBaseFromMousePosition();
                _heldPlacement?.UpdateHeldPosition();
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)  _heldPlacement?.TryPlaceHeldBlocks();
                if (mouse != null && mouse.rightButton.wasPressedThisFrame) _extraction?.CancelHold();
            }
            else
            {
                _heldPlacement?.UpdateHeldPosition();
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)  _extraction?.CancelHold();
                if (mouse != null && mouse.rightButton.wasPressedThisFrame) _extraction?.CancelHold();
            }
        }
        else
        {
            _holdStartedByMouse = false;
            HandleSelectionKeyboardInput(keyboard);
            RefreshHoldingTransition();
            bool wasHolding = _held != null && _held.IsHolding;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _extraction?.HandleClick();
                if (!wasHolding && _held != null && _held.IsHolding)
                {
                    _holdStartedByMouse = true;
                    _held.UsingKeyboardPlacement = false;
                }
            }
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) _extraction?.ClearSelection();
        }

        float scroll = mouse?.scroll.ReadValue().y ?? 0f;
        if (Mathf.Abs(scroll) > 0.01f)
            _camera?.ScrollCamera(scroll);

        _camera?.UpdateCameraTarget();
        _camera?.UpdateSecondaryViewCamera();
    }

    #endregion

    #region Keyboard Mode

    void HandleSelectionKeyboardInput(Keyboard kb)
    {
        if (!keyboardControlsEnabled || kb == null) return;
        _physicsController?.RefreshDetachedComponents();

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
            _extraction?.ClearSelection();
            _selection?.ClearFocus();
            return;
        }

        _selection?.EnsureFocusedCell(_extraction);

        if (_selection != null && _selection.IsPresetSelectionActive)
        {
            _selection.HandlePresetSelectionInput(_extraction, hasMove, dir, hasConfirm, hasPreset, preset, hasTab, hasPresetRotate, hasPresetHalfTurn);
            return;
        }

        if (hasPreset)
        {
            _extraction?.ClearSelection();
            if (_selection != null && _selection.HasFocusedCell) _selection.BeginPresetSelection(_extraction, preset);
            return;
        }

        if (hasMove)    _selection?.MoveFocus(_extraction, dir);
        if (hasConfirm)
        {
            if (_selection != null && _selection.Selected.Count >= 4)
                _extraction?.LiftBlocks();
            else if (_selection != null && _selection.HasFocusedCell)
                _selection.ToggleFocusedSelection(_extraction);
        }
    }

    void HandleHeldKeyboardInput(Keyboard kb)
    {
        if (!keyboardControlsEnabled || kb == null) return;

        if (ReadMoveHeld(kb, allowWasd: true, out var dir))
        {
            if (_held != null && !_held.UsingKeyboardPlacement) _heldPlacement?.InitKeyboardPlacement();
            _heldPlacement?.MoveHeldBase(dir);
        }

        if (ReadHalfTurnPressed(kb))
        {
            _held?.RotateHeldBlocks(clockwise: true);
            _held?.RotateHeldBlocks(clockwise: true);
            if (_held != null && _held.UsingKeyboardPlacement)
                _held.BaseCell = _placement?.ClampHeldBase(_held.BaseCell) ?? _held.BaseCell;
        }
        else if (ReadHeldRotationPressed(kb, out var clockwise))
        {
            _held?.RotateHeldBlocks(clockwise);
            if (_held != null && _held.UsingKeyboardPlacement)
                _held.BaseCell = _placement?.ClampHeldBase(_held.BaseCell) ?? _held.BaseCell;
        }

        if (ConfirmPressed(kb) && Time.unscaledTime >= _placementConfirmAllowedAt)
            _heldPlacement?.DropHeldToNearestSurfaceAndPlace();
        if (CancelPressed(kb))  _extraction?.CancelHold();
    }

    void RefreshHoldingTransition()
    {
        bool isHolding = _held != null && _held.IsHolding;
        if (isHolding && !_wasHolding)
            _placementConfirmAllowedAt = Time.unscaledTime + placementConfirmDelay;
        _wasHolding = isHolding;
    }

    #endregion

    #region 입력 읽기 헬퍼

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
            _moveRepeatDir      = Vector2Int.zero;
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
        kb.backspaceKey.wasPressedThisFrame;

    #endregion
}
