extends Node

@onready var mainui_scene: PackedScene = preload("res://UI/MainUI.tscn")


func _ready() -> void:
	add_child(mainui_scene.instantiate())
