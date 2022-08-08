extends Control

onready var debug_console = $Console
onready var tax_window = $TaxWindow
onready var advsior_meet_window = $AdvisorMeet
onready var tools_window = $ToolsWindow

const ticker_path = "res://json/ticker/"
const fnn_logo = "res://assets/ticker/fnn.png"

onready var ticker_text = $Status/NewsBtn
onready var ticker_box = $NewsWindow/News
onready var ticker_window = $NewsWindow
onready var news_brand = $NewsWindow/Brand/NewsBrand
onready var news_motto = $NewsWindow/Brand/NewsMotto

var other_outlet: String = ""
var news_file: String = ""
var rng = RandomNumberGenerator.new()
var all_news = []
var speices = [
	"Cat",
	"Fennec",
	"Fox"
]

var json_files = [
	"adverts.json",
	"sammy.json",
	"kittykibble.json",
	"citylife.json"
]

func _competing_outlet():
	var file = File.new()
	var caseyverse_path = str(ticker_path + "caseyverse.json");	
	if file.file_exists(caseyverse_path):
		file.open(caseyverse_path)
		var result = parse_json(file.get_as_text())
		result.clear()
		return result["competing_outlet"]

func _load_json():
	var file = File.new()	
	if file.file_exists(news_file):
		file.open(news_file, file.READ)
		var result = parse_json(file.get_as_text())
		return result

func _index_news():
	var news = _load_json()
	all_news.clear()
	all_news = news["ticker"]	
	randomize()
	all_news.shuffle()

func _ready():
	SimData.city_name = SimData.city_name.capitalize()
	SimData.mayor_name = SimData.mayor_name.capitalize()
	
	if SimData.city_name == "Furtropolis" and Caseyverse.is_caseyverse():
		news_brand.texture = load(fnn_logo)
	
	SimEvents.emit_signal("advisor_message", SimData.Advisors.CITY_PLANNER, 0)
	
	SimEvents.connect("send_alert", self, "_start_alert")
	SimEvents.connect("rotate_news", self, "_rotate_news")
	
	if Caseyverse.is_caseyverse():
		json_files.append("extra_lore.json")
	
	_randomize_news(json_files)

func _process(delta):
	if Input.is_action_pressed("ui_cheats"):
		debug_console.show()
		
	var prev_json_Files = json_files
	
#	var city_life = [
#		"citylife.json"
#	]
#
#	if _array_check(city_life, json_files):
#		match SimData.has_power:
#			true: 
#				prev_json_Files = json_files
#				for files in city_life:
#					json_files.append(files)
#
#			false: 
#				prev_json_Files = json_files
#				for files in city_life:
#					json_files.remove(files)

			
func _array_check(list1, list2):
	for item in list1:
		if item	in list2:
			return true
	
	return false

func _start_alert(message):
#	if ticker_text.items.size() > 1:
#		ticker_text.clear()
#
#	SimData.on_alert = true
#	news_file = str(ticker_path + "ticker_alerts.json")
#	ticker_text.add_item(all_news)
	
	pass

func _randomize_news(files: Array):	
	for file in files:
		news_file = str(ticker_path + file)
		_load_json()
		_index_news()
		
	rng.randomize()
	randomize()
	files.shuffle()
	
	var news_range = rng.randi_range(0, all_news.size() - 1)
	var news_text: String = all_news[news_range]
	
	if Caseyverse.is_caseyverse():
		news_text = news_text.replace("[other_outlet]", Caseyverse.competing_outlet())
	
	if SimData.city_name == "Furtropolis" or "Furville" and Caseyverse.is_caseyverse():
		# FNN = Furtropolis/Furry News Network
		news_text = news_text.replace("[outlet]", "FNN")
	else:
		news_text = news_text.replace("[outlet]", "Pawprint Press")

	if "[species]" in news_text:
		speices.shuffle()
		var speices_range = rng.randi_range(speices.size() - 1)
		news_text = news_text.replace("[species]", speices[speices_range])

	if "[city]" in news_text:
		news_text = news_text.replace("[city]", SimData.city_name)

	if "[mayor]" in news_text:
		news_text = news_text.replace("[mayor]", SimData.mayor_name)
	
	if ticker_box.items.size() > 15:
		ticker_box.clear()
	
	# Prevent duplicates
	var prev_news_text = ticker_text.text
	if news_text == prev_news_text:
		_randomize_news(json_files)
	else:
		json_files.shuffle()
		_add_news(news_text)

func _add_news(news_item):
	ticker_text.text = news_item
	ticker_box.add_item(news_item)
	
func _on_RotateNews_timeout():
	rng.randomize()
	randomize()
	all_news.shuffle()
	json_files.shuffle()
	_randomize_news(json_files)

func _on_TaxBtn_pressed():
	tax_window.show()

func _on_AdvsiorBtn_pressed():
	advsior_meet_window.show()

func _on_ToolsBtn_pressed():
	tools_window.show()

func _on_NewsBtn_pressed():
	ticker_window.show()
