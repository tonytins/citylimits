extends Control

const TICKER_PATH = "res://json/ticker/"
const FNN_LOGO = "res://assets/ticker/fnn.png"
const CONFIG_FILE = "config.json"

onready var ticker_text = $Status/NewsBtn
onready var ticker_box = $Windows/NewsWindow/News
onready var ticker_window = $Windows/NewsWindow

var news_file: String = ""
var rng = RandomNumberGenerator.new()
var all_news: Array = []
var speices: Array = [
	"Cat",
	"Fennec",
	"Fox"
]

var json_files: Array = []

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
	ticker_window.window_title = JsonHelper.key_value(TICKER_PATH, CONFIG_FILE, "outlet")
	
	_randomize_news(json_files)

#func _process(delta):
#	var prev_json_Files = json_files
#
#	var city_life = [
#		"citylife.json",
#		"kittykibble.json",
#		"international.json",
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
#					json_files.append(files)

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
#	news_file = str(TICKER_PATH + "ticker_alerts.json")
#	ticker_text.add_item(all_news)
	
	pass

func _randomize_news(files: Array):	
	if all_news == null:
		json_files = JsonHelper.key_value(TICKER_PATH, CONFIG_FILE, "ticker_files")
		
	for file in files:
		news_file = str(TICKER_PATH + file)
		_load_json()
		_index_news()
		
	rng.randomize()
	randomize()
	files.shuffle()
	
	var news_range = rng.randi_range(0, all_news.size() - 1)
	var news_text: String = all_news[news_range]
	
	if "[competing_outlet]" in news_text:
		news_text = news_text.replace("[competing_outlet]", JsonHelper.key_value(TICKER_PATH, CONFIG_FILE, "competing_outlet"))
	
	if "[outlet]" in news_text:	
		news_text = news_text.replace("[outlet]", JsonHelper.key_value(TICKER_PATH, CONFIG_FILE, "outlet"))

	if "[species]" in news_text:
		speices.shuffle()
		var speices_range = rng.randi_range(speices.size() - 1)
		news_text = news_text.replace("[species]", speices[speices_range])

	if "[city]" in news_text:
		news_text = news_text.replace("[city]", SimData.city_name)

	if "[mayor]" in news_text:
		news_text = news_text.replace("[mayor]", SimData.mayor_name)
	
	# Prevent stack overflaw
	if ticker_box.items.size() > 10:
		ticker_box.clear()
	
	_randomize_news(json_files)
	_add_news(news_text)

func _add_news(news_item):
	ticker_text.text = news_item
	ticker_box.add_item(news_item)
	
func _on_NewsBtn_pressed():
	ticker_window.show()
	
func _on_RotateNews_timeout():
	rng.randomize()
	randomize()
	all_news.shuffle()
	json_files.shuffle()
	_randomize_news(json_files)
