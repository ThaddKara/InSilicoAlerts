# InSilicoAlerts

 * This project is trading bot based off InSilico indicators
 * Deribit (ethereum)
 
 ## Requirements
 
 * AWS account with api access key/value pair
 * TradingView Pro account (and access to InSilico indicators)
 
 ## deployment (VS)
 
 * Clone repo
 * Create new DynamoDB table named **InSilico** and primary-key named **key**
 * Add this object to the table: **{key: your deribit key, secret: your deribit secret, webhook: webhook password}**
 * Open the InSilicoAlerts solution using VisualStudio and right click on the project file
 * Click on deploy to AWS Lambda, create a unique bucket name and stack name then click on deploy
 * In tradingview create the webhook alert for Fisher values above -10 (yes we want to capture every candles fisher value)
 * After deployment, your webhook url should appear. copy it and append **/tv?key=webhookpassword** where webhook password is the same as in the Dynamo table
 * Ex: https://1da1231daaqe.execute-api.us-east-1.amazonaws.com/Prod/tv?key=password
 
