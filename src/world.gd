extends Node2D

var noise : OpenSimplexNoise
var map_size = Vector2(80, 60)
var grass_cap = 0.5
var road_caps = Vector2(0.3, 0.05)

func _ready():
	randomize()
	noise = OpenSimplexNoise.new()
	noise.seed = randi()
	noise.octaves = 1.5
	noise.period = 12
	
	make_terrian_map()


func make_terrian_map():
	for x in map_size.x:
		for y in map_size.y:
			var a = noise.get_noise_2d(x, y)
			if a < grass_cap:
				$Terrian.set_cell(x, y, 0)

	$Terrian.update_bitmask_region(Vector2(0.0, 0.0), Vector2(map_size.x, map_size.y))