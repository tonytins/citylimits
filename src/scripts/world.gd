extends Node

var noise: OpenSimplexNoise
var map_size = Vector2(80, 60)
var terrian_cap = 0.3

func _process(delta):
	
	$GUI/GPanel/HBox/MoneyLbl.text = str(bank.budget)
	
func _input(event):
	pass
	
func _ready():
	
	bank.starting_budget(1)
	
	randomize()
	noise = OpenSimplexNoise.new()
	noise.seed = randi()
	noise.octaves = 1.5
	noise.period = 12
	
	make_terrian_map()
	make_water()

func make_terrian_map():
	for x in map_size.x:
		for y in map_size.y:
			var a = noise.get_noise_2d(x, y)
			if a < terrian_cap:
				$Terrian.set_cell(x, y, 0)

	$Terrian.update_bitmask_region(Vector2(0.0, 0.0), Vector2(map_size.x, map_size.y))
	
func make_water():
	for x in map_size.x:
		for y in map_size.y:
			if $Terrian.get_cell(x, y):
				$Water.set_cell(x, y, 0)
				
	$Water.update_bitmask_region(Vector2(0.0, 0.0), Vector2(map_size.x, map_size.y))