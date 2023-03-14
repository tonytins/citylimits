@icon("../icons/state.png")
class_name State
extends Node

## The bare, basic state. Use it if you want total control over the state-flow.
##
## Properties marked as [b](inherited)[/b] are passed to substates,
## meaning you don't have to set it on each individual state, only the root.
## You can override it of course, and that will be passed to all of [i]its[/i] children.


## Emitted between [method _enter] and [method _after_enter].
signal entered

## Emitted after [method _exit].
signal exited

## Emitted between [method _update] and [method _after_update]
signal updated

## Switched active substates.
signal active_substate_changed(new: State, old: State)

## A request for the parent to pick a new substate to activate.
## Mainly used by children of [RandomState], such as an [AnimationState].
signal choose_new_substate_requested


## Active or not.
enum Status {
	INACTIVE, ## Inactive
	ACTIVE, ## Active
	}


@export
## The node that the states will act upon. [b](inherited)[/b]
## Doesn't actually get used in the addon scripts, it's just
## included for your convenience when scripting your own behaviour.
var target: Node:
	set(value):
		target = value
		if _active_substate != null: 
			_active_substate.target = target

@export
## Where to play animations from. [b](inherited)[/b]
var animation_player: AnimationPlayer

@export_range(0, 120, 1, "or_greater")
## How many seconds the state should be active before emitting [signal choose_new_substate_requested].
## [b]If set to zero, it will go forever.[/b]
var timer := 0.0

@export
## Whether to force-restart the chosen substate in the callback for [signal choose_new_substate_requested] if it was already active.
var force := true

@export
## The state will not be activated under any circumstances.
var disabled := false:
	set(value):
		disabled = value
		var root := is_root()
		if root and not disabled: 
			enter(target, animation_player, debug_mode)
		elif status == Status.ACTIVE: 
			exit()

@export
## Print a message avery time there is a state change. [b](inherited)[/b]
var debug_mode := false:
	set(value):
		debug_mode = value
		if _active_substate != null: 
			_active_substate.debug_mode = debug_mode

## The status of this state, ie. whether it's running or not.
var status := Status.INACTIVE


# The substate that is currently active, if any.
var _active_substate: State:
	set(value):
		if _active_substate != null:
			_active_substate.choose_new_substate_requested.disconnect(_on_choose_new_substate_requested)
		active_substate_changed.emit(value, _active_substate)
		_active_substate = value
		if _active_substate != null:
			_active_substate.choose_new_substate_requested.connect(_on_choose_new_substate_requested)

# If a timer is set, the object will be stored here.
var _timer_object: SceneTreeTimer


#########################
### VIRTUAL METHODS ###
#########################
func _init() -> void:
	set_physics_process(false)
	set_meta(&"description", "A bare, basic state - will only ever automatically start its first child.")


func _ready() -> void:
	for child in get_children():
		assert(child is State, "A State should not have any children that are not other States.")
	if is_root() and not disabled:
		enter(target, animation_player, debug_mode)


func _physics_process(delta: float) -> void:
	if status == Status.INACTIVE:
		set_physics_process(false)
		return
	update(delta)


## [b][parents, then children][/b] Called when the state is activated.
func _enter() -> void:
	pass


## [b][children, then parents][/b] Called after the state is activated.
func _after_enter() -> void:
	pass


## [b][parents, then children][/b] Called every physics frame (only when the state is active, of course).
func _update(delta: float) -> void:
	pass


## [b][children, then parents][/b] Called at the end of every physics frame.
func _after_update(delta: float) -> void:
	pass


## [b][parents, then children][/b] Called before the state is deactivated.
func _before_exit() -> void:
	pass


## [b][children, then parents][/b] Called when the state is deactivated.
func _exit() -> void:
	pass


## You can define which state is picked automatically (like on [method enter]).
## Return `null` to not change substate at all.
## If you would like to call it yourself, use the public version ([method choose_substate]).
func _choose_substate() -> State:
	return get_child(0) as State if get_child_count() > 0 else null


########################
### PUBLIC METHODS ###
########################

## Switch to the specified substate by name. It is just a shortcut to [method change_state_node].
func change_state_name(name: String, force := false) -> State:
	return await change_state_node(get_node_or_null(name) as State, force)


## Switch to the specified substate by node. If it is not a direct child, nothing will happen.
## If `force`, it will start a state again even if it's already running.
## It waits for the next [signal updated] to make sure it's not
## switching all over the place in one tick.
func change_state_node(node: State, force := false) -> State:
	await updated
	if (
			node == null 
			or node.disabled
			or (node.status != Status.INACTIVE and not force) 
			or node.get_parent() != self
			):
		return node

	var old := _active_substate
	_active_substate = node
	if old != null:
		old.exit()
	_active_substate.enter(target, animation_player, debug_mode)

	if debug_mode:
		print(
				("FORCE " if force else "") +
				"STATE: " +
				str(get_root().get_parent().get_path_to(_active_substate))
				)
	return _active_substate


## Return the currently active substate, if any.
func get_active_substate() -> State:
	return _active_substate


## Public [method _choose_substate].
func choose_substate() -> State:
	return _choose_substate()


## Shortcut for `change_state_node(choose_substate())`.
func change_to_next_substate(force := false) -> void:
	await change_state_node(choose_substate(), force)


## Whether this state is the root of the state tree,
## ie. it is the common ancestor of all the others.
func is_root() -> bool:
	# If your parent is not a state, then you are the root.
	return not get_parent() is State


## Get the root state.
func get_root() -> State:
	var node: State = self
	while not node.is_root(): 
		node = node.get_parent() as State
	return node


## Runs [method _enter] and [method _after_enter],
## not a good idea to call it yourself unless you really know what you're doing.
func enter(set_target: Node, set_animation_player: AnimationPlayer, set_debug_mode: bool) -> void:
	for child in get_children():
		assert(child is State, "A State should not have any children that are not other States.")
	
	_enter()
	entered.emit()
	status = Status.ACTIVE
	if timer != 0:
		_timer_object = get_tree().create_timer(timer)
		_timer_object.timeout.connect(_on_timer_timeout)
	set_physics_process(is_root())

	# Only set them if they're not being overridden
	if target == null: 
		target = set_target
	if animation_player == null:
		animation_player = set_animation_player
	if debug_mode == false: 
		debug_mode = set_debug_mode

	change_to_next_substate()
	_after_enter()


## Runs [method _update] and [method _after_update],
## not a good idea to call it yourself unless you really know what you're doing.
func update(delta: float) -> void:
	_update(delta)
	updated.emit()
	if _active_substate != null:
		_active_substate.update(delta)
	_after_update(delta)


## Runs [method _exit] and [method _before_exit],
## not a good idea to call it yourself unless you really know what you're doing.
func exit() -> void:
	_before_exit()
	status = Status.INACTIVE
	if _active_substate != null: 
		_active_substate.exit()
	_active_substate = null
	
	if is_instance_valid(_timer_object):
		_timer_object.timeout.disconnect(_on_timer_timeout)
		_timer_object = null
	
	_exit()
	exited.emit()
	set_physics_process(false)


#########################
### PRIVATE METHODS ###
#########################


#################
### CALLBACKS ###
#################

func _on_choose_new_substate_requested() -> void:
	change_to_next_substate(force)


func _on_timer_timeout() -> void:
	choose_new_substate_requested.emit()
