extends Node3D

@onready var _player: CharacterBody3D = $"../PlayerCharacterBody3D"
@onready var _player_model: Node3D = $"../PlayerCharacterBody3D/PlayerModel"
@onready var _spring_arm: SpringArm3D = $SpringArm3D
@onready var _camera: Camera3D = $SpringArm3D/Camera3D

var _model_focus_offset: Vector3 = Vector3.ZERO


func _ready() -> void:
	_model_focus_offset = _compute_model_focus_offset()


func _process(delta: float) -> void:
	var focus_point: Vector3 = _player.to_global(_model_focus_offset)
	global_position = focus_point
	_camera.look_at(focus_point, Vector3.UP)




@export_range(0.0, 1.0) var mouse_sensitivity: float = 0.01
@export var tilt_limit: float = deg_to_rad(75)
@export var focus_height: float = 1.35
@export var zoom_step: float = 1.0
@export var min_zoom: float = 1.5
@export var max_zoom: float = 14.0
var is_rotating: bool = false


func _compute_model_focus_offset() -> Vector3:
	var points: Array[Vector3] = []
	_collect_mesh_bound_points(_player_model, points)

	if points.is_empty():
		return Vector3.UP * focus_height

	var min_point: Vector3 = points[0]
	var max_point: Vector3 = points[0]

	for point in points:
		min_point = min_point.min(point)
		max_point = max_point.max(point)

	return (min_point + max_point) * 0.5


func _collect_mesh_bound_points(node: Node, points: Array[Vector3]) -> void:
	if node is MeshInstance3D:
		var mesh_instance := node as MeshInstance3D
		var mesh_bounds: AABB = mesh_instance.get_aabb()

		for corner in _get_aabb_corners(mesh_bounds):
			points.append(_player.to_local(mesh_instance.to_global(corner)))

	for child in node.get_children():
		_collect_mesh_bound_points(child, points)


func _get_aabb_corners(bounds: AABB) -> Array[Vector3]:
	var position: Vector3 = bounds.position
	var size: Vector3 = bounds.size

	return [
		position,
		position + Vector3(size.x, 0.0, 0.0),
		position + Vector3(0.0, size.y, 0.0),
		position + Vector3(0.0, 0.0, size.z),
		position + Vector3(size.x, size.y, 0.0),
		position + Vector3(size.x, 0.0, size.z),
		position + Vector3(0.0, size.y, size.z),
		position + size,
	]

func _input(event):
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		if event.pressed:
			is_rotating = true
		else:
			is_rotating = false

	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			_spring_arm.spring_length = clampf(_spring_arm.spring_length - zoom_step, min_zoom, max_zoom)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_spring_arm.spring_length = clampf(_spring_arm.spring_length + zoom_step, min_zoom, max_zoom)
			
func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and is_rotating:
		rotation.x -= event.relative.y * mouse_sensitivity
		# Prevent the camera from rotating too far up or down.
		rotation.x = clampf(rotation.x, -tilt_limit, tilt_limit)
		rotation.y += -event.relative.x * mouse_sensitivity
