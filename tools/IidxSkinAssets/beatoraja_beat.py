"""Original opacity curves driven by beatoraja 0.8.9's public rhythm clock.

OMS timing.beat is continuous chart musical time and freezes during STOP.
Native TIMER_RHYTHM (140) is a visual clock: one elapsed second represents a
beat, speed follows RhythmTimerProcessor, and section lines reset its phase.
STOP does not freeze it. This adapter reads that actual host clock; it does not
integrate current BPM or claim the original OMS musical phase.
"""

from copy import deepcopy
import math

from beatoraja_native import Lua


BEAT_ADAPTATION = {
    "code": "native-beat-opacity-adaptation",
    "detail": "Original linear opacity keys use public TIMER_RHYTHM 140: one native elapsed second per beat, "
              "including host speed and section resets. Unlike OMS continuous timing.beat, the host keeps "
              "pulsing through STOP; section, practice-frequency and BPM-update phase follow beatoraja. "
              "No BPM integration is performed. Native time is quantized to 1/1000 beat and key alpha to "
              "one byte. Before PLAY the source first value is retained. A termination observed in its "
              "own render freezes that host phase; late termination freezes the last observed phase, "
              "or the first value when no PLAY phase was observed. No earlier phase is reconstructed.",
}


def _keys(track):
    keys = track.get("keyframes", [])
    if (track.get("type") != "tween" or track.get("clock") != "timing.beat"
            or track.get("property") != "opacity" or track.get("loop") is not True
            or track.get("easing", "linear") != "linear" or len(keys) < 2):
        raise ValueError("Unqualified native beat opacity track")
    for key in keys:
        if (any(isinstance(key.get(field), bool) or not isinstance(key.get(field), (int, float))
                or not math.isfinite(key[field]) for field in ("time", "value"))
                or not 0 <= key["value"] <= 1
                or abs(key["time"] * 1000 - round(key["time"] * 1000)) > 1e-7):
            raise ValueError("Beat opacity keys require finite alpha and exact native millibeats")
    if (keys[0]["time"] != 0 or keys[-1]["time"] != 1 or keys[0]["value"] != keys[-1]["value"]
            or any(a["time"] >= b["time"] for a, b in zip(keys, keys[1:]))):
        raise ValueError("Beat opacity must be a closed, ordered one-beat source curve")
    return keys


def beat_opacity_track(node, scene):
    """Find and qualify this sprite's actual track; unrelated nodes return None."""
    tracks = [track for track in scene.get("tracks", [])
              if track.get("target") == node["id"] and track.get("clock") == "timing.beat"]
    if not tracks:
        return None
    if node["type"] != "sprite" or len(tracks) != 1:
        raise ValueError("Native beat adaptation requires one original sprite opacity track")
    track = tracks[0]
    keys = _keys(track)
    if (node.get("properties", {}).get("opacity", 1) != keys[0]["value"]
            or any(other is not track and other.get("target") == node["id"]
                   and other.get("property") == "opacity" for other in scene.get("tracks", []))
            or any(operation.get("target") == node["id"] and operation.get("property") == "opacity"
                   for kind in ("bindings", "variants") for operation in scene.get(kind, []))):
        raise ValueError("Beat sprite opacity has an unqualified competing source or OFF pose")
    return track


def rhythm_timer():
    """A read-only TimerProperty, with native millisecond arithmetic explicit.

READY is the epoch identity: PLAY's origin moves during speed changes. Native
140 also moves, and resets at measures, so neither is a restart identity.
The callback can be read repeatedly in one render without accumulating time.
"""
    return Lua("""(function()
  local off=main_state.timer_off_value
  local previous_time,previous_ready,last_phase,frozen=nil,nil,nil,nil
  return function()
    local now=main_state.time()
    local ready=main_state.timer(40)
    local play=main_state.timer(41)
    local rhythm=main_state.timer(140)
    if (previous_time~=nil and now<previous_time)
        or (previous_ready~=nil and ready~=previous_ready) then
      last_phase,frozen=nil,nil
    end
    previous_time,previous_ready=now,ready
    local phase=0
    if play==off or rhythm==off then
      last_phase,frozen=nil,nil
    else
      local terminal=off
      for _,id in ipairs({3,908,2}) do
        local stamp=main_state.timer(id)
        if stamp~=off and stamp<=now and (terminal==off or stamp<terminal) then terminal=stamp end
      end
      if terminal~=off then
        if frozen==nil then
          if terminal==now then frozen=math.max(0,now-rhythm)
          else frozen=last_phase or 0 end
        end
        phase=frozen
      else
        phase=math.max(0,now-rhythm)
        last_phase=phase
        frozen=nil
      end
    end
    -- SkinObject subtracts two signed integer-millisecond values. Quantize
    -- the elapsed beat once, avoiding a second moving boundary on freeze.
    return (math.floor(now/1000)-math.floor(phase/1000))*1000
  end
end)()""")


def apply_beat_opacity(destination, track, carrier_alpha):
    """Replace only one static sprite's alpha track, retaining all other fields.

The caller first sets this node's opacity to one while resolving colour, then
passes that unrounded alpha here. The source key value replaces node opacity;
it must not multiply the already-rounded .35 initial destination a again.
"""
    keys = _keys(track)
    if (isinstance(carrier_alpha, bool) or not isinstance(carrier_alpha, (int, float))
            or not math.isfinite(carrier_alpha) or not 0 <= carrier_alpha <= 1):
        raise ValueError("Beat opacity requires the real finite carrier alpha")
    points = destination.get("dst", [])
    if (destination.get("timer", 0) not in (0, None) or destination.get("loop", 0) != 0
            or len(points) != 1 or points[0].get("time", 0) != 0 or points[0].get("acc", 0) != 0
            or points[0].get("a", 255) != round(carrier_alpha * 255)):
        raise ValueError("Beat opacity cannot replace another clock or a pre-multiplied alpha")
    result = deepcopy(destination)
    result.update(timer=rhythm_timer(), loop=0, dst=[
        {**deepcopy(points[0]), "time": round(key["time"] * 1000), "acc": 0,
         "a": round(carrier_alpha * key["value"] * 255)} for key in keys])
    return result
