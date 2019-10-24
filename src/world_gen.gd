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
	
	make_grass_map()
	make_road_map()


func make_grass_map():
	for x in map_size.x:
		for y in map_size.y:
			var a = noise.get_noise_2d(x, y)
			if a < grass_cap:
				$Grass.set_cell(x, y, 0)
				
	$Grass.update_bitmask_region(Vector2(0.0, 0.0), Vector2(map_size.x, map_size.y))


func make_road_map():
	for x in map_size.x:
		for y in map_size.y:
			var a = noise.get_noise_2d(x, y)
			if a < road_caps.x and a > road_caps.y: 
				$Roads.set_cell(x, y, 0)
				
	$Roads.update_bitmask_region(Vector2(0.0, 0.0), Vector2(map_size.x, map_size.y))
