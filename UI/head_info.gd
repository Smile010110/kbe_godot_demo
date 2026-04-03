extends Node3D


func _process(_delta: float) -> void:
	var camera := get_viewport().get_camera_3d()
	if camera == null:
		return

	var cam_pos = camera.global_transform.origin
	var my_pos = global_transform.origin
	cam_pos.y = my_pos.y
	look_at(cam_pos, Vector3.UP)
	rotate_y(PI)
