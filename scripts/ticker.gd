extends ColorRect

onready var ticker_text = $TickerTxt

var news_file = ""
var rng = RandomNumberGenerator.new()
var news_keys = []

func _index_news():
	var news = _load_news()
	news_keys.clear()
	for key in news:
		news_keys.append(news[key])

func _load_news():
	var file = File.new()
	if file.file_exists(news_file):
		file.open(news_file, file.READ)
		var dialogue = parse_json(file.get_as_text())
		return dialogue

func _ready():
	SimEvents.connect("send_alert", self, "_start_alert")
	SimEvents.connect("resume_news", self, "_resume_ticker")
	_random_news("res://dialog/ticker.json")

func _process(delta):
	if get_tree().paused:
		ticker_text.text = "Simulation Paused"

func _start_alert(message):
	SimData.is_alert = true
	news_file = "res://dialog/ticker_alerts.json"
	ticker_text.text = news_keys[message].text

func _random_news(file):
	news_file = file
	_load_news()
	_index_news()
	rng.randomize()
	var max_mange = news_keys.size() - 1
	var ticker_range = rng.randi_range(0, max_mange)
	var news = news_keys[ticker_range].text
	
	if SimData.city_name == "Furtropolis" and "[outlet]" in news:
		# FNN = Furtropolis News Network
		news = news.replace("[outlet]", "FNN")
	elif "[outlet]" in news:
		news = news.replace("[outlet]", "Pawprint Press")
	
	if "[city]" in news:
		news = news.replace("[city]", SimData.city_name)
	
	if "[mayor]" in news:
		news = news.replace("[mayor]", SimData.mayor_name)
	
	ticker_text.text = news
	
func _resume_ticker():
	_random_news("res://dialog/ticker.json")
	
func _on_RotateNews_timeout():
	_random_news("res://dialog/ticker.json")
