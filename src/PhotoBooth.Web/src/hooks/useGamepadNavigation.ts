import { useEffect, useRef, useMemo } from 'react';
import type { GamepadButtonsConfig, GamepadDpadAxesConfig } from '../api/types';

export interface GamepadDebugEvent {
  gamepadId: string;
  /** Set for button events */
  buttonIndex?: number;
  /** Set for axis events */
  axisIndex?: number;
  action: string;
}

export interface GamepadNavigationConfig {
  onNext?: () => void;
  onPrevious?: () => void;
  onSkipForward?: () => void;
  onSkipBackward?: () => void;
  onToggleMode?: () => void;
  onTriggerCapture?: (durationMs?: number) => void;
  enabled?: boolean;
  debugMode?: boolean;
  buttons?: GamepadButtonsConfig;
  dpadAxes?: GamepadDpadAxesConfig | null;
  onDebugEvent?: (event: GamepadDebugEvent) => void;
}

const DEFAULT_BUTTONS: GamepadButtonsConfig = {
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

function buildActionMap(buttons: GamepadButtonsConfig): Map<number, string> {
  const map = new Map<number, string>();
  for (const idx of buttons.next) map.set(idx, 'next');
  for (const idx of buttons.previous) map.set(idx, 'previous');
  for (const idx of buttons.skipForward) map.set(idx, 'skipForward');
  for (const idx of buttons.skipBackward) map.set(idx, 'skipBackward');
  for (const idx of buttons.triggerCapture) map.set(idx, 'triggerCapture');
  for (const idx of buttons.triggerCapture1s) map.set(idx, 'triggerCapture1s');
  for (const idx of buttons.triggerCapture3s) map.set(idx, 'triggerCapture3s');
  for (const idx of buttons.triggerCapture5s) map.set(idx, 'triggerCapture5s');
  for (const idx of buttons.toggleMode) map.set(idx, 'toggleMode');
  return map;
}

export function useGamepadNavigation({
  onNext,
  onPrevious,
  onSkipForward,
  onSkipBackward,
  onToggleMode,
  onTriggerCapture,
  enabled = true,
  debugMode = false,
  buttons = DEFAULT_BUTTONS,
  dpadAxes = null,
  onDebugEvent,
}: GamepadNavigationConfig): void {
  // Keep refs for all mutable values so the RAF callback always has fresh values
  // without needing to be re-registered on every prop change.
  const callbacksRef = useRef({ onNext, onPrevious, onSkipForward, onSkipBackward, onToggleMode, onTriggerCapture, onDebugEvent });
  const enabledRef = useRef(enabled);
  const debugModeRef = useRef(debugMode);
  const actionMapRef = useRef<Map<number, string>>(new Map());
  const dpadAxesRef = useRef(dpadAxes);

  // Build action map (memoized to avoid rebuilding unless buttons config changes)
  const actionMap = useMemo(() => buildActionMap(buttons), [buttons]);

  // Sync all mutable values into refs after every render so the RAF polling
  // callback always reads the latest props without needing to be re-registered.
  useEffect(() => {
    callbacksRef.current = { onNext, onPrevious, onSkipForward, onSkipBackward, onToggleMode, onTriggerCapture, onDebugEvent };
    enabledRef.current = enabled;
    debugModeRef.current = debugMode;
    actionMapRef.current = actionMap;
    dpadAxesRef.current = dpadAxes;
  });

  // Set up RAF polling once on mount, tear down on unmount
  useEffect(() => {
    const prevButtonStates = new Map<number, boolean[]>();
    // Axis virtual-button states: key = `${gamepadIndex}_${axisIndex}_${direction}`
    const prevAxisStates = new Map<string, boolean>();
    let rafId: number;

    function fireAction(action: string) {
      if (!enabledRef.current || action === 'unmapped') return;
      const cbs = callbacksRef.current;
      switch (action) {
        case 'next': cbs.onNext?.(); break;
        case 'previous': cbs.onPrevious?.(); break;
        case 'skipForward': cbs.onSkipForward?.(); break;
        case 'skipBackward': cbs.onSkipBackward?.(); break;
        case 'triggerCapture': cbs.onTriggerCapture?.(); break;
        case 'triggerCapture1s': cbs.onTriggerCapture?.(1000); break;
        case 'triggerCapture3s': cbs.onTriggerCapture?.(3000); break;
        case 'triggerCapture5s': cbs.onTriggerCapture?.(5000); break;
        case 'toggleMode': cbs.onToggleMode?.(); break;
      }
    }

    function poll() {
      const gamepads = navigator.getGamepads();

      for (const gamepad of gamepads) {
        if (!gamepad) continue;

        // --- Button polling ---
        const prevStates = prevButtonStates.get(gamepad.index) ?? [];
        const newStates = gamepad.buttons.map(b => b.pressed);

        for (let i = 0; i < gamepad.buttons.length; i++) {
          const wasPressed = prevStates[i] ?? false;
          const isPressed = newStates[i];

          // Only fire on press edge (not-pressed → pressed), not on hold
          if (!wasPressed && isPressed) {
            const action = actionMapRef.current.get(i) ?? 'unmapped';

            // Debug mode fires for all presses (including unmapped), regardless of enabled
            if (debugModeRef.current) {
              callbacksRef.current.onDebugEvent?.({ gamepadId: gamepad.id, buttonIndex: i, action });
            }

            fireAction(action);
          }
        }

        prevButtonStates.set(gamepad.index, newStates);

        // --- Axis-based D-pad polling ---
        const axesCfg = dpadAxesRef.current;
        if (axesCfg) {
          const checkAxis = (axisIdx: number, positive: boolean, action: string) => {
            const key = `${gamepad.index}_${axisIdx}_${positive ? 'pos' : 'neg'}`;
            const value = gamepad.axes[axisIdx] ?? 0;
            const isActive = positive ? value > axesCfg.threshold : value < -axesCfg.threshold;
            const wasActive = prevAxisStates.get(key) ?? false;
            prevAxisStates.set(key, isActive);

            if (!wasActive && isActive) {
              if (debugModeRef.current) {
                callbacksRef.current.onDebugEvent?.({ gamepadId: gamepad.id, axisIndex: axisIdx, action });
              }
              fireAction(action);
            }
          };

          checkAxis(axesCfg.horizontalAxisIndex, true, 'next');
          checkAxis(axesCfg.horizontalAxisIndex, false, 'previous');
          checkAxis(axesCfg.verticalAxisIndex, true, 'skipForward');
          checkAxis(axesCfg.verticalAxisIndex, false, 'skipBackward');
        }
      }

      rafId = requestAnimationFrame(poll);
    }

    rafId = requestAnimationFrame(poll);

    return () => {
      cancelAnimationFrame(rafId);
    };
  }, []);
}
