import { useEffect, useRef, useMemo } from 'react';
import type { GamepadButtonsConfig } from '../api/types';

export interface GamepadDebugEvent {
  gamepadId: string;
  buttonIndex: number;
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
  onDebugEvent?: (event: GamepadDebugEvent) => void;
}

const DEFAULT_BUTTONS: GamepadButtonsConfig = {
  next: [5, 15],
  previous: [4, 14],
  skipForward: [3, 13],
  skipBackward: [2, 12],
  triggerCapture: [0],
  toggleMode: [8],
};

function buildActionMap(buttons: GamepadButtonsConfig): Map<number, string> {
  const map = new Map<number, string>();
  for (const idx of buttons.next) map.set(idx, 'next');
  for (const idx of buttons.previous) map.set(idx, 'previous');
  for (const idx of buttons.skipForward) map.set(idx, 'skipForward');
  for (const idx of buttons.skipBackward) map.set(idx, 'skipBackward');
  for (const idx of buttons.triggerCapture) map.set(idx, 'triggerCapture');
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
  onDebugEvent,
}: GamepadNavigationConfig): void {
  // Keep refs for all mutable values so the RAF callback always has fresh values
  // without needing to be re-registered on every prop change.
  const callbacksRef = useRef({ onNext, onPrevious, onSkipForward, onSkipBackward, onToggleMode, onTriggerCapture, onDebugEvent });
  const enabledRef = useRef(enabled);
  const debugModeRef = useRef(debugMode);
  const actionMapRef = useRef<Map<number, string>>(new Map());

  // Build action map (memoized to avoid rebuilding unless buttons config changes)
  const actionMap = useMemo(() => buildActionMap(buttons), [buttons]);

  // Update refs on every render so the polling callback always reads latest values
  callbacksRef.current = { onNext, onPrevious, onSkipForward, onSkipBackward, onToggleMode, onTriggerCapture, onDebugEvent };
  enabledRef.current = enabled;
  debugModeRef.current = debugMode;
  actionMapRef.current = actionMap;

  // Set up RAF polling once on mount, tear down on unmount
  useEffect(() => {
    const prevButtonStates = new Map<number, boolean[]>();
    let rafId: number;

    function poll() {
      const gamepads = navigator.getGamepads();

      for (const gamepad of gamepads) {
        if (!gamepad) continue;

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
              callbacksRef.current.onDebugEvent?.({
                gamepadId: gamepad.id,
                buttonIndex: i,
                action,
              });
            }

            if (enabledRef.current && action !== 'unmapped') {
              const cbs = callbacksRef.current;
              switch (action) {
                case 'next': cbs.onNext?.(); break;
                case 'previous': cbs.onPrevious?.(); break;
                case 'skipForward': cbs.onSkipForward?.(); break;
                case 'skipBackward': cbs.onSkipBackward?.(); break;
                case 'triggerCapture': cbs.onTriggerCapture?.(); break;
                case 'toggleMode': cbs.onToggleMode?.(); break;
              }
            }
          }
        }

        prevButtonStates.set(gamepad.index, newStates);
      }

      rafId = requestAnimationFrame(poll);
    }

    rafId = requestAnimationFrame(poll);

    return () => {
      cancelAnimationFrame(rafId);
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
}
