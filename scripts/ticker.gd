extends ColorRect

onready var ticker_text = $TickerTxt

const ticker_path = "res://json/ticker/"

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
	# "sammy.json"
]

func _index_news():
	var news = _load_json()
	all_news.clear()
	all_news = news["ticker"]
		
	randomize()
	all_news.shuffle()
	
func _process(delta):
	var prev_json_Files = json_files
	
	var city_life = [
		"citylife.json",
		"kittykibble.json"
	]
	
	if _array_check(city_life, json_files):
		match SimData.has_power:
			true: 
				prev_json_Files = json_files
				for files in city_life:
					json_files.append(files)
					
			false: 
				prev_json_Files = json_files
				for files in city_life:
					json_files.append(files)
			
func _array_check(list1, list2):
	for item in list1:
		if item	in list2:
			return true
	
	return false

func _load_json():
	var file = File.new()
	if file.file_exists(news_file):
		file.open(news_file, file.READ)
		var result = parse_json(file.get_as_text())
		return result
		
func _ready():
	SimData.city_name = SimData.city_name.capitalize()
	SimData.mayor_name = SimData.mayor_name.capitalize()
	SimEvents.connect("send_alert", self, "_start_alert")
	SimEvents.connect("resume_news", self, "_resume_ticker")
	_random_news(json_files)

func _start_alert(message):
	SimData.on_alert = true
	news_file = str(ticker_path + "ticker_alerts.json")
	ticker_text = all_news

func _random_news(files: Array):
	for file in files:
		news_file = str(ticker_path + file)
		_load_json()
		_index_news()
	
	rng.randomize()
	randomize()
	all_news.shuffle()
	
	var news_range = rng.randi_range(0, all_news.size() - 1)
	var news_text: String = all_news[news_range]
	
	if SimData.has_ctower or SimData.city_name == "Furtropolis" or "Furville" and "[outlet]" in news_text:
		# FNN = Furtropolis/Furry News Network
		news_text.replace("[outlet]", "FNN")
	else:
		news_text.replace("[outlet]", "Pawprint Press")

	if "[species]" in news_text:
		randomize()
		speices.shuffle()
		var speices_range = rng.randi_range(speices.size() - 1)
		news_text.replace("[species]", speices[speices_range])

	if "[city]" in news_text:
		news_text.replace("[city]", SimData.city_name)

	if "[mayor]" in news_text:
		news_text.replace("[mayor]", SimData.mayor_name)
	
	ticker_text.text = news_text
	
func _resume_ticker():
	_random_news(json_files)
	
func _on_RotateNews_timeout():
	_random_news(json_files)
