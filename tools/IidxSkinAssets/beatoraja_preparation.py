"""Source preparation clocks observed through public beatoraja timers.

Each native CustomTimer owns an independent copy of the same small state
machine. It reads host state once per render and never writes gameplay timers.
The skin cache supplies all subsequent geometry/visibility reads.
"""

import math

from beatoraja_native import Lua


PREPARATION_TIMER = 10020
PRESENTATION_TIMER = 10021
READY_TIMER = 10022
ACTIVE_TIMER = 10023


def _timer(identifier, duration):
    # 10023 stores the countdown's end, not an elapsed-clock origin. This lets
    # expressions read remaining time without an implicit shared duration/global.
    result = {
        PREPARATION_TIMER: "now - preparation",
        PRESENTATION_TIMER: "now - presentation",
        READY_TIMER: "ready_origin",
        ACTIVE_TIMER: "active_end",
    }[identifier]
    body = """(function()
  local off=main_state.timer_off_value
  local duration=__DURATION__
  local epoch,rejected,previous_time=off,off,nil
  local preparation_final,play_initial,frozen=nil,nil,nil
  local previous_failure=off
  local function invalidate(ready, failure)
    epoch,rejected=off,ready
    preparation_final,play_initial,frozen=nil,nil,nil
    previous_failure=failure
  end
  return function()
    local now=main_state.time()
    local ready=main_state.timer(40)
    local play=main_state.timer(41)
    local fade=main_state.timer(2)
    local failure=main_state.timer(3)
    local practice=main_state.option(1080)
    local loading=main_state.option(80)
    local rewound=previous_time~=nil and now<previous_time
    previous_time=now
    if rewound or practice or loading or ready==off then
      invalidate(ready,failure)
      return off
    end

    local preparation,presentation
    local ready_origin,active_end=off,off
    if play==off then
      -- READY40 survives practice configuration and early cancellation. Its
      -- old timestamp cannot begin another epoch when those gates disappear.
      if fade~=off or failure~=off or (epoch==ready and play_initial~=nil) then
        invalidate(ready,failure)
        return off
      end
      if epoch~=ready then
        if rejected==ready then return off end
        epoch=ready
        preparation_final,play_initial,frozen=nil,nil,nil
        previous_failure=failure
      end
      preparation=math.max(0,now-ready)
      presentation=preparation
      active_end=ready+duration
      local remaining=math.max(0,active_end-now)
      if remaining<=1500000 then
        ready_origin=now-(1500000-remaining)
      end
    else
      -- A late PLAY snapshot reveals neither its practice start offset nor
      -- the earlier preparation history. Wait for a real observed READY.
      if epoch~=ready then
        invalidate(ready,failure)
        return off
      end
      if play_initial==nil then
        preparation_final=math.max(0,now-ready)
        play_initial=now-play
      end
      preparation=preparation_final
      presentation=preparation+math.max(0,now-play-play_initial)
      if frozen==nil and failure~=off and failure~=previous_failure then
        frozen=preparation+math.max(0,failure-play-play_initial)
      end
      previous_failure=failure
      if frozen~=nil then presentation=frozen end
    end
    return __RESULT__
  end
end)()"""
    return {"id": identifier, "timer": Lua(body.replace("__DURATION__", str(duration * 1000))
                                          .replace("__RESULT__", result))}


def preparation_plan(frame):
    """Return the selected frame's native waiting time and cached clock owners.

The verified authors declare whole milliseconds. Native playstart is an
integer, so another source must not silently lose a fractional declaration.
Zero/absent preparation creates no clocks and invents no author waiting phase.
"""
    duration = frame.scene.get("preparationDuration", 0)
    if (isinstance(duration, bool) or not isinstance(duration, (int, float))
            or not math.isfinite(duration) or not 0 <= duration <= 10000
            or duration != int(duration)):
        raise ValueError("Native preparation requires a source duration of 0..10000 whole milliseconds")
    duration = int(duration)
    result = {"customTimers": [], "playstart": duration, "gaps": []}
    if duration == 0:
        return result
    result["customTimers"] = [_timer(identifier, duration) for identifier in
                              (PREPARATION_TIMER, PRESENTATION_TIMER, READY_TIMER, ACTIVE_TIMER)]
    result["gaps"] = [
        {"code": "native-preparation-clock-adaptation", "informational": True,
         "detail": "Source preparation duration owns native READY waiting. Ready uses only the final 1500 ms; the native strict integer-ms > transition can oversample the waiting endpoint by render cadence. Preparation then freezes at the actual first PLAY sample. Presentation subtracts the observed initial PLAY elapsed (including practice offset), follows later native PLAY speed, and freezes at a new failure timestamp. Native loading/start controls replace OMS preparation hold and pause; no host timers are written."},
        {"code": "native-preparation-unobserved-epoch", "informational": True,
         "detail": "A first PLAY snapshot without an observed READY epoch leaves all preparation/presentation clocks unavailable until a new READY. It does not invent preparation history or assume a practice start offset. Each owner updates through the native per-render CustomTimer cache, before drawable reads."},
    ]
    return result


def preparation_expression(source):
    """Return a public cached-clock expression and its OMS value kind.

Unavailable numeric values are zero, matching the source snapshot. Recorded
animation availability must still use the corresponding timer's OFF state.
"""
    boolean = {"preparation.available": PREPARATION_TIMER,
               "preparation.active": ACTIVE_TIMER, "preparation.ready": READY_TIMER}
    if source in boolean:
        return f"(main_state.timer({boolean[source]}) ~= main_state.timer_off_value)", "boolean"
    clocks = {"preparation.elapsed": PREPARATION_TIMER,
              "presentation.elapsed": PRESENTATION_TIMER,
              "preparation.ready-elapsed": READY_TIMER,
              "preparation.remaining": ACTIVE_TIMER}
    if source not in clocks:
        return None
    difference = "t-main_state.time()" if source == "preparation.remaining" else "main_state.time()-t"
    return (f"(function() local t=main_state.timer({clocks[source]}); "
            f"if t==main_state.timer_off_value then return 0 end; "
            f"return math.max(0,({difference})/1000) end)()", "number")
