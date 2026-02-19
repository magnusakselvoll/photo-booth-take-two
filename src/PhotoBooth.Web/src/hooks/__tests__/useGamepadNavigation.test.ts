import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, cleanup } from '@testing-library/react';
import { useGamepadNavigation } from '../useGamepadNavigation';

// ---- helpers ---------------------------------------------------------------

function makeButton(pressed: boolean): GamepadButton {
  return { pressed, touched: pressed, value: pressed ? 1 : 0 };
}

function makeGamepad(
  index: number,
  buttonStates: boolean[],
  id = 'test-gamepad',
): Gamepad {
  return {
    id,
    index,
    buttons: buttonStates.map(makeButton),
    axes: [],
    connected: true,
    mapping: 'standard' as GamepadMappingType,
    timestamp: 0,
    hapticActuators: [],
    vibrationActuator: null,
  } as unknown as Gamepad;
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
});
