extends Panel

func _ready():
	$citymenus/citynamelbl.text = CityData.city_name

func _process(delta):
	$citystatus/moneylbl.text = str(CityData.budget)
