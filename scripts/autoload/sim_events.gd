extends Node

signal has_power
signal budget
signal game_speed

signal policy_message(policy)
signal policy_analysis(advisor, policy)
signal advisor_message(advisor, message)

# News
signal send_alert(message)
signal resume_news

# Policies
signal clean_air_act
signal energy_saving
