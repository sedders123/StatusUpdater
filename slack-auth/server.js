const express = require("express");
const querystring = require("querystring");
const CryptoJS = require("crypto-js");
const { DateTime } = require("luxon");
const { response } = require("express");
const axios = require("axios").default;

const app = express();
const PORT = process.env.PORT || 3000;

const SLACK_AUTHORIZE_URL = "https://slack.com/oauth/v2/authorize";
const SLACK_ACCESS_TOKEN_URL = "https://slack.com/api/oauth.v2.access";
const SECRET_KEY = process.env.SECRET_KEY;

app.get("/", (req, res) => {
  const state = CryptoJS.AES.encrypt(
    DateTime.local().valueOf().toString(),
    SECRET_KEY
  ).toString();
  const params = {
    client_id: process.env.CLIENT_ID,
    user_scope: process.env.SCOPE,
    redirect_uri: process.env.REDIRECT_URI,
    state: state,
  };
  res.redirect(`${SLACK_AUTHORIZE_URL}?${querystring.stringify(params)}`);
});

app.get("/authorise", (req, res) => {
  const state = parseInt(
    CryptoJS.AES.decrypt(req.query.state, SECRET_KEY).toString(
      CryptoJS.enc.Utf8
    )
  );
  if (DateTime.local().minus({ minutes: 5 }) > DateTime.fromMillis(state)) {
    // state is more than 5 minutes old or invalid
    res.sendStatus(500);
  }
  const data = {
    client_id: process.env.CLIENT_ID,
    client_secret: process.env.CLIENT_SECRET,
    code: req.query.code,
    redirect_uri: process.env.REDIRECT_URI,
  };
  console.log(querystring.stringify(data));
  axios
    .post(SLACK_ACCESS_TOKEN_URL, querystring.stringify(data), {
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
    })
    .then((response) => {
      res.setHeader("Content-Type", "application/json");
      res.json(response.data);
    })
    .catch((error) => {
      console.error("error posting json: ", error);
      res.sendStatus(500);
    });
});

app.listen(PORT, () => {
  console.log("Listening on port 3000");
});
