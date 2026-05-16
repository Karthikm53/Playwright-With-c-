Feature: Search

@SearchData
Scenario: Search Data
	Given I navigate to the application url
	When I search some data
	Then I close the browser