# Illustrative game pseudocode

This is engine-independent example logic for the documented game sequence. It is not executable code. Movement, communications pulses, and attack effects are authored animation cues; no drone hardware, radio model, or flight controller is involved.

## Example state

```text
phase = ADVANCE
primary = eight fictional drone tokens
for drone in primary:
    drone.available = true
    drone.visible = true
    drone.shielded = false
tail = []
followUp = []
handledCheckpoints = set()
minimumPrimary = 1
initialOrders = immutableCopy([actionFor(A), actionFor(B), actionFor(C)])
# Fixed before departure. These authored anchors are also used by the primary pass.
cachedRepeatOrders = immutableCopy([
    actionFor(A, primaryA1ImpactAnchor),
    actionFor(B, primaryB1ImpactAnchor),
    actionFor(C, primaryC1ImpactAnchor)
])
linkAvailable = true
levelRun = newRunIdentifier()

objectives = { A: UNTOUCHED, B: UNTOUCHED, C: UNTOUCHED }

# Outcomes are supplied by the storyboard, not calculated combat behavior.
primaryOutcomes  = { A: CLEARED, B: DAMAGED, C: DAMAGED }
followUpOutcomes = { A: CLEARED, B: CLEARED, C: CLEARED }
```

Each animation event carries its level-run identifier. Ignore events from an old run after a reset. Each action completion also has a unique key so repeated animation callbacks cannot apply its result twice.

## Form the tail at authored checkpoints

```text
onCheckpointReached(checkpoint, eventRun):
    if eventRun != levelRun or phase != ADVANCE:
        return
    if checkpoint.id in handledCheckpoints:
        return

    if countAvailable(primary) <= minimumPrimary:
        failSequence("Checkpoint requires another primary token")
        return

    drone = lastAvailableToken(primary)    # Stable roster order for the example.
    primary.remove(drone)
    drone.role = RELAY
    drone.shielded = true
    tail.append(drone)
    handledCheckpoints.add(checkpoint.id)

    emit RelayAdded(drone, checkpoint.animationAnchor)
    emit RiseThenCircleAtAuthoredHeight(drone, checkpoint.animationAnchor)
    emit ShowArcadeShield(drone)
    emit RedrawCommsChain(groundTeam, tail, primary)
```

The checkpoint, higher hover level, and small circular path are placed by the level author. The communication lines follow each relay's current animated position. With eight tokens and three checkpoints, the roster becomes five primary tokens plus three relay tokens.

## Show communications

```text
onScriptedLinkChanged(isAvailable, eventRun):
    if eventRun != levelRun or phase not in [ADVANCE, PRIMARY_PASS]:
        return
    linkAvailable = isAvailable
    emit SetCommsAppearance(isAvailable)

onStatusOrOrderMessage(message, eventRun):
    if eventRun != levelRun or phase not in [ADVANCE, PRIMARY_PASS]:
        return
    if not linkAvailable:
        emit MessageWaiting(message)
        return
    emit AnimateMessagePulse(message.direction)
    # Display the message; never replace the initial-order snapshot here.
```

The primary and repeat plans are already stored before departure. The repeat plan contains only the authored earlier A/B/C hit positions. Objective-state displays and later messages never rewrite it. In this example, a broken link changes the display and blocks new message displays, while the authored sequence continues from its stored plan. The animation may show a waiting message being sent again after reconnection; no networking implementation is implied.

## Draw the illustrative range overlay

```text
for drone in sampledFrame.drones:
    if drone.visible:
        drawTranslucentRedCircleFor(drone, designerChosenArtRadius)
    else:
        hideRangeCircleFor(drone)
```

The radius is fixed game artwork in arbitrary scene units. It does not use radio parameters, terrain obstruction, or a real-world range calculation. The map's contour lines are scenery annotations.

## Run a pass, then reuse the original orders

```text
onPrimaryArrival(eventRun):
    if eventRun != levelRun or phase != ADVANCE:
        return
    phase = PRIMARY_PASS
    startPass(PRIMARY_PASS, primary, initialOrders, primaryOutcomes)

onPrimaryPassFinished(eventRun):
    if eventRun != levelRun or phase != PRIMARY_PASS:
        return                          # Prevent a second release.
    phase = FOLLOW_UP

    followUp = availableTokens(tail)
    tail.clear()
    for drone in followUp:
        drone.role = FOLLOW_UP
        drone.shielded = false
        emit HideArcadeShield(drone)
        emit DescendFromCurrentCirclePosition(drone)
    emit TailReleased(followUp)          # Animate a staggered departure.
    emit SetCommsAppearance(TAIL_RELEASED)

    replayOrders = cachedRepeatOrders
    # No objective-state filtering, target selection, or new order information.
    # Repeat the cached A, B, and C hits. Never add targets after departure.

    if followUp is empty or replayOrders is empty:
        finishSequence()
        return
    startPass(FOLLOW_UP, followUp, replayOrders, followUpOutcomes)
```

`startPass` is an abstract animation sequencer with this contract:

```text
Capture levelRun as passRun and the requested phase as passPhase when starting.
Use the storyboard's fixed drone assignments: a drone may appear in only one
attack assignment in the entire playback.

For each action, in the supplied order:
    Stop if levelRun != passRun or phase != passPhase.
    Stop with a setup error if the action's authored scene entity is missing.
    Keep the cached action even if the objective is already CLEARED.
    For each available drone assigned to this action:
        Cue its authored diving animation once.
        On the matching impact event:
            Ignore it if levelRun != passRun or phase != passPhase.
            Ignore duplicate impact keys or an already consumed drone.
            Mark the impact key handled before emitting effects.
            Emit a small explosion at the authored impact position.
            drone.available = false
            drone.visible = false
            drone.shielded = false
            drone.role = DESTROYED
            Hide its body, trail, and any shield immediately.
            Apply the outcome attached to this authored impact cue.
    Record that action as completed once.

When this pass's list is exhausted:
    Stop if levelRun != passRun or phase != passPhase.
    If passPhase == PRIMARY_PASS, emit PrimaryPassFinished(passRun).
    If passPhase == FOLLOW_UP, finishSequence().
```

Replaying an order creates a new action attempt assigned to a previously unused relay drone at the exact earlier impact position. It does not revive a consumed attacker. The fixed follow-up list runs once for the wave; each assigned drone makes at most one attack. No action is chosen from the current objective states.

In the current storyboard, two primary drones attack A, two attack B, and one attacks C. Each former relay then repeats one earlier primary hit, in A/B/C order and at the same authored impact position. Every attack hits an objective. A is repeated despite already being cleared; B and C clear after their repeat impacts. Outcomes and impact points remain authored animation events.

## Finish, fail, and reset

```text
finishSequence():
    phase = COMPLETE
    cancelPendingActionCues()
    markAvailableTokensFinished()
    # Never restore a DESTROYED token here. This storyboard ends with eight consumed.
    emit ShowFinalObjectiveStates(objectives)

failSequence(reason):
    phase = FAILED
    cancelPendingActionCues()
    emit ShowFailure(reason)

resetSequence():
    cancelPendingActionCues()
    levelRun = newRunIdentifier()        # Old callbacks now have no effect.
    restoreInitialRostersAndObjectives()
    restoreAllEightBodiesAndAvailability()
    clearExplosionsTrailsAndShields()
    clearCheckpointFlagsAndActionCompletionKeys()
    restoreInitialOrdersAndLinkDisplay()
    restoreCachedRepeatOrders()
    phase = ADVANCE
```

## Storyboard checks

- Three checkpoint events move exactly three of eight tokens into the tail; repeating an event changes nothing.
- Elevated relays circle their authored points while the visible link follows their positions; protection exists only during the relay role.
- Each attack hits an assigned objective and consumes exactly one drone at its impact cue.
- An explosion never appears before its impact cue, and consumed bodies and trails stay hidden afterward.
- The cached repeat sequence is A, then B, then C, each at an earlier primary hit position.
- Both passes hit all three objectives, with all three cleared in the final frame.
- A is repeated even when already cleared; changing the displayed objective state cannot choose a new action.
- Repeating the primary-finished event does not create another wave.
- A missing authored scene entity ends with a setup error instead of substituting another objective.
- No available relays, or no cached actions, ends the sequence without spawning tokens.
- Failure stops pending actions; reset restores the starting state and ignores callbacks from the previous run.

These examples remain explanatory pseudocode. The Unity implementation samples its complete state directly from the playhead, so backward seeking and reset reconstruct the earlier drone, objective, orbit, and effect states. Current test results are recorded in the reports described in [Play the animation](PLAY_ANIMATION.md#verification).
