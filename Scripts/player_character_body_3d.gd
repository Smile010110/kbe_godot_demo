extends CharacterBody3D

const JUMP_VELOCITY = 4.5

@onready var _camera_pivot := $"../CameraPivot"


func _physics_process(delta: float) -> void:
	var player_controller := $".."
	if player_controller.GetStatus() != 0:
		return

	var move_speed: float = float(player_controller.GetMoveSpeed())
	var input_dir := Vector2.ZERO

	if Input.is_action_pressed("move_forward"):
		input_dir.y -= 1
	if Input.is_action_pressed("move_backward"):
		input_dir.y += 1
	if Input.is_action_pressed("move_left"):
		input_dir.x -= 1
	if Input.is_action_pressed("move_right"):
		input_dir.x += 1

	input_dir = input_dir.normalized()

	if input_dir != Vector2.ZERO:
		var camera_yaw_basis := Basis(Vector3.UP, _camera_pivot.rotation.y)
		var move_dir := (camera_yaw_basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()

		velocity.x = move_dir.x * move_speed
		velocity.z = move_dir.z * move_speed
		look_at(global_position + Vector3(move_dir.x, 0, move_dir.z), Vector3.UP)
	else:
		velocity.x = 0.0
		velocity.z = 0.0

	if not is_on_floor():
		velocity.y -= 9.8 * delta
	else:
		if Input.is_action_just_pressed("jump"):
			velocity.y = JUMP_VELOCITY
		else:
			velocity.y = 0.0

	move_and_slide()
