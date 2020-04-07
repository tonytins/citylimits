extends Panel

func _ready():
	$CityMenus/CityNameLbl.text = CityData.city_name

func _process(delta):
	$CityStatus/MoneyLbl.text = str(CityData.budget)
