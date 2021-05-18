extends AcceptDialog

onready var avatar_texture = $Container/Advisor/Avatar
onready var rank_label = $Container/Advisor/RankLbl
onready var name_label = $Container/Advisor/NameLbl
onready var description_label = $Container/DescriptionLbl

export var advisor_name: String
export var rank: String
export var title: String
export(String, MULTILINE) var description
export(Texture) var avatar

func _ready():
	self.window_title = title
	description_label.text = description
	avatar_texture.texture = avatar
	name_label.text = advisor_name
	rank_label.text = rank
