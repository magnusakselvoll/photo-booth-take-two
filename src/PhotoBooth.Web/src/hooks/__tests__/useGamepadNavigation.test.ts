import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, cleanup } from '@testing-library/react';
import { useGamepadNavigation } from '../useGamepadNavigation';
import type { GamepadButtonsConfig, GamepadDpadAxesConfig } from '../../api/types';

// ---- helpers ---------------------------------------------------------------

function makeButton(pressed: boolean): GamepadButton {
  return { pressed, touched: pressed, value: pressed ? 1 : 0 };
}

function makeGamepad(
  index: number,
  buttonStates: boolean[],
  id = 'test-gamepad',
  axes: number[] = [],
): Gamepad {
  return {
    id,
    index,
    buttons: buttonStates.map(makeButton),
    axes,
    connected: true,
    mapping: 'standard' as GamepadMappingType,
    timestamp: 0,
    hapticActuators: [],
    vibrationActuator: null,
  } as unknown as Gamepad;
}

const DEFAULT_BUTTONS_FOR_TEST: GamepadButtonsConfig = {
  next: [5, 15],
  previous: [4, 14],
  skipForward: [3, 13],
  skipBackward: [2, 12],
  triggerCapture: [0],
  triggerCapture1s: [],
  triggerCapture3s: [],
  triggerCapture5s: [],
  toggleMode: [8],
};

const DEFAULT_DPAD_AXES: GamepadDpadAxesConfig = {
  horizontalAxisIndex: 6,
  verticalAxisIndex: 7,
  threshold: 0.5,
};

/** Build an 8-element axis array with D-pad at indices 6 and 7 */
function axes(h = 0, v = 0): number[] {
  const arr = new Array<number>(8).fill(0);
  arr[6] = h;
  arr[7] = v;
  return arr;
}

/** 16-element button array, all false except the given indices */
function pressed(...indices: number[]): boolean[] {
  const arr = new Array<boolean>(16).fill(false);
  for (const i of indices) arr[i] = true;
  return arr;
}

// ---- RAF / navigator mocks -------------------------------------------------

let rafCallback: FrameRequestCallback | null = null;
let rafIdCounter = 0;
let mockGetGamepads: ReturnType<typeof vi.fn<() => (Gamepad | null)[]>>;

function tick() {
  rafCallback?.(0);
}

beforeEach(() => {
  rafCallback = null;
  rafIdCounter = 0;

  vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
    rafCallback = cb;
    return ++rafIdCounter;
  });
  vi.stubGlobal('cancelAnimationFrame', vi.fn());

  mockGetGamepads = vi.fn<() => (Gamepad | null)[]>().mockReturnValue([]);
  Object.defineProperty(navigator, 'getGamepads', {
    value: mockGetGamepads,
    configurable: true,
    writable: true,
  });
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

// ---- tests -----------------------------------------------------------------

describe('useGamepadNavigation', () => {
  it('calls onNext when a mapped next button is pressed', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onNext }));

    // First tick: establish initial state (all not-pressed)
    tick();
    expect(onNext).not.toHaveBeenCalled();

    // Second tick: button 5 (RB / default Next) pressed
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5))]);
    tick();
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('calls onNext when the other default next button (15, D-Right) is pressed', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onNext }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(15))]);
    tick();
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('calls onPrevious when a mapped previous button is pressed', () => {
    const onPrevious = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onPrevious }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(4))]);
    tick();
    expect(onPrevious).toHaveBeenCalledOnce();
  });

  it('calls onSkipForward when button 3 is pressed', () => {
    const onSkipForward = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onSkipForward }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(3))]);
    tick();
    expect(onSkipForward).toHaveBeenCalledOnce();
  });

  it('calls onSkipBackward when button 2 is pressed', () => {
    const onSkipBackward = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onSkipBackward }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(2))]);
    tick();
    expect(onSkipBackward).toHaveBeenCalledOnce();
  });

  it('calls onTriggerCapture when button 0 (A/Cross) is pressed', () => {
    const onTriggerCapture = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onTriggerCapture }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(0))]);
    tick();
    expect(onTriggerCapture).toHaveBeenCalledOnce();
  });

  it('calls onToggleMode when button 8 (Back/Share) is pressed', () => {
    const onToggleMode = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onToggleMode }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(8))]);
    tick();
    expect(onToggleMode).toHaveBeenCalledOnce();
  });

  it('does not repeat-fire while a button is held (only on press edge)', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onNext }));

    tick(); // establish initial state

    // Hold button 5 across three ticks
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5))]);
    tick(); // press edge → fires
    tick(); // still held → no fire
    tick(); // still held → no fire

    expect(onNext).toHaveBeenCalledOnce();
  });

  it('fires again after release and re-press', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onNext }));

    tick(); // initial state

    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5))]);
    tick(); // press → fires (1)

    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    tick(); // release

    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5))]);
    tick(); // press again → fires (2)

    expect(onNext).toHaveBeenCalledTimes(2);
  });

  it('suppresses actions when enabled is false', () => {
    const onNext = vi.fn();
    const onTriggerCapture = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onNext, onTriggerCapture, enabled: false }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5, 0))]);
    tick();

    expect(onNext).not.toHaveBeenCalled();
    expect(onTriggerCapture).not.toHaveBeenCalled();
  });

  it('fires onDebugEvent for mapped buttons when debugMode is true', () => {
    const onDebugEvent = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'my-gamepad')]);
    renderHook(() => useGamepadNavigation({ debugMode: true, onDebugEvent }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5), 'my-gamepad')]);
    tick();

    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onDebugEvent).toHaveBeenCalledWith({
      gamepadId: 'my-gamepad',
      buttonIndex: 5,
      action: 'next',
    });
  });

  it('fires onDebugEvent for unmapped buttons when debugMode is true', () => {
    const onDebugEvent = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ debugMode: true, onDebugEvent }));

    tick();
    // Button 7 is not in the default mapping
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(7))]);
    tick();

    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onDebugEvent).toHaveBeenCalledWith(
      expect.objectContaining({ buttonIndex: 7, action: 'unmapped' }),
    );
  });

  it('fires onDebugEvent even when enabled is false', () => {
    const onDebugEvent = vi.fn();
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ enabled: false, debugMode: true, onDebugEvent, onNext }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5))]);
    tick();

    // Debug event fires, but action does not
    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onNext).not.toHaveBeenCalled();
  });

  it('calls onTriggerCapture with 1000 when a TriggerCapture1s button is pressed', () => {
    const onTriggerCapture = vi.fn();
    const customButtons = { ...DEFAULT_BUTTONS_FOR_TEST, triggerCapture1s: [6] };
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onTriggerCapture, buttons: customButtons }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(6))]);
    tick();
    expect(onTriggerCapture).toHaveBeenCalledWith(1000);
  });

  it('calls onTriggerCapture with 3000 when a TriggerCapture3s button is pressed', () => {
    const onTriggerCapture = vi.fn();
    const customButtons = { ...DEFAULT_BUTTONS_FOR_TEST, triggerCapture3s: [7] };
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onTriggerCapture, buttons: customButtons }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(7))]);
    tick();
    expect(onTriggerCapture).toHaveBeenCalledWith(3000);
  });

  it('calls onTriggerCapture with 5000 when a TriggerCapture5s button is pressed', () => {
    const onTriggerCapture = vi.fn();
    const customButtons = { ...DEFAULT_BUTTONS_FOR_TEST, triggerCapture5s: [9] };
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onTriggerCapture, buttons: customButtons }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(9))]);
    tick();
    expect(onTriggerCapture).toHaveBeenCalledWith(5000);
  });

  it('cancels animation frame on unmount', () => {
    mockGetGamepads.mockReturnValue([]);
    const { unmount } = renderHook(() => useGamepadNavigation({}));

    tick(); // run one poll (schedules next RAF)
    unmount();

    expect(vi.mocked(cancelAnimationFrame)).toHaveBeenCalled();
  });

  it('respects custom button mapping', () => {
    const onNext = vi.fn();
    const onPrevious = vi.fn();
    const customButtons = {
      next: [1],
      previous: [2],
      skipForward: [],
      skipBackward: [],
      triggerCapture: [],
      triggerCapture1s: [],
      triggerCapture3s: [],
      triggerCapture5s: [],
      toggleMode: [],
    };
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    renderHook(() => useGamepadNavigation({ onNext, onPrevious, buttons: customButtons }));

    tick();

    // Custom next button pressed
    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(1))]);
    tick();
    expect(onNext).toHaveBeenCalledOnce();
    expect(onPrevious).not.toHaveBeenCalled();

    // Default next button (5) should NOT fire next with custom mapping
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false))]);
    tick(); // release

    mockGetGamepads.mockReturnValue([makeGamepad(0, pressed(5))]);
    tick();
    expect(onNext).toHaveBeenCalledOnce(); // still only once
  });

  // ---- D-pad axis tests ----

  it('calls onNext when horizontal axis goes positive (D-pad right)', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onNext, dpadAxes: DEFAULT_DPAD_AXES }));

    tick(); // establish initial state
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(1, 0))]);
    tick();
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('calls onPrevious when horizontal axis goes negative (D-pad left)', () => {
    const onPrevious = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onPrevious, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(-1, 0))]);
    tick();
    expect(onPrevious).toHaveBeenCalledOnce();
  });

  it('calls onSkipForward when vertical axis goes positive (D-pad down)', () => {
    const onSkipForward = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onSkipForward, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 1))]);
    tick();
    expect(onSkipForward).toHaveBeenCalledOnce();
  });

  it('calls onSkipBackward when vertical axis goes negative (D-pad up)', () => {
    const onSkipBackward = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onSkipBackward, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, -1))]);
    tick();
    expect(onSkipBackward).toHaveBeenCalledOnce();
  });

  it('does not repeat-fire while axis is held (only on edge)', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onNext, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(1, 0))]);
    tick(); // edge → fires
    tick(); // still held → no fire
    tick(); // still held → no fire

    expect(onNext).toHaveBeenCalledOnce();
  });

  it('does not fire axis action when enabled is false', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onNext, enabled: false, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(1, 0))]);
    tick();
    expect(onNext).not.toHaveBeenCalled();
  });

  it('fires onDebugEvent with axisIndex for axis events in debugMode', () => {
    const onDebugEvent = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ debugMode: true, onDebugEvent, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(1, 0))]);
    tick();

    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onDebugEvent).toHaveBeenCalledWith({ gamepadId: 'gp', axisIndex: 6, action: 'next' });
  });

  it('fires onDebugEvent for unconfigured axis (joystick stick) in debugMode', () => {
    const onDebugEvent = vi.fn();
    // Axes 0 and 1 are the joystick stick; dpadAxes is configured for axes 6 and 7
    const stickAxes = new Array<number>(8).fill(0);
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', stickAxes)]);
    renderHook(() => useGamepadNavigation({ debugMode: true, onDebugEvent, dpadAxes: DEFAULT_DPAD_AXES }));

    tick(); // establish initial state
    const movedAxes = [...stickAxes];
    movedAxes[0] = 1; // push stick right on axis 0 (unconfigured)
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', movedAxes)]);
    tick();

    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onDebugEvent).toHaveBeenCalledWith({ gamepadId: 'gp', axisIndex: 0, action: 'unmapped' });
  });

  it('fires onDebugEvent for all axes when dpadAxes is null in debugMode', () => {
    const onDebugEvent = vi.fn();
    const stickAxes = new Array<number>(4).fill(0);
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', stickAxes)]);
    renderHook(() => useGamepadNavigation({ debugMode: true, onDebugEvent, dpadAxes: null }));

    tick();
    const movedAxes = [...stickAxes];
    movedAxes[0] = 1; // push axis 0
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', movedAxes)]);
    tick();

    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onDebugEvent).toHaveBeenCalledWith({ gamepadId: 'gp', axisIndex: 0, action: 'unmapped' });
  });

  it('does not double-fire configured axes in debugMode', () => {
    const onDebugEvent = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ debugMode: true, onDebugEvent, dpadAxes: DEFAULT_DPAD_AXES }));

    tick();
    // Move the configured horizontal axis (index 6) positive
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(1, 0))]);
    tick();

    // Should fire exactly once (from configured axis polling, not duplicated by debug loop)
    expect(onDebugEvent).toHaveBeenCalledOnce();
    expect(onDebugEvent).toHaveBeenCalledWith({ gamepadId: 'gp', axisIndex: 6, action: 'next' });
  });

  it('does not fire axis actions when dpadAxes is null', () => {
    const onNext = vi.fn();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(0, 0))]);
    renderHook(() => useGamepadNavigation({ onNext, dpadAxes: null }));

    tick();
    mockGetGamepads.mockReturnValue([makeGamepad(0, new Array<boolean>(16).fill(false), 'gp', axes(1, 0))]);
    tick();
    expect(onNext).not.toHaveBeenCalled();
  });
});
