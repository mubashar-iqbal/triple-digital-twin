// SPDX-License-Identifier: GPL-3.0
pragma solidity >=0.7.0 <0.9.0;

/**
* This smart contract defines the safety and security rules 
* for TRIPLE digital twins implemented using Microsoft
* Azure Digital Twins (ADT) platform.
*/
contract Triple_DigitalTwins_SnS_Rules {

    /**
     * Minimum allowed and safe temperature during working mode.
     */
    int minTemp;

    /**
     * Maxium allowed and safe temperature during working mode.
     */
    int maxTemp;

    /**
    * System should set the minimum and maximum threshold for allowed
    * and safe temperature during working mode.
    */
    function SetTemperatureThreshold(int _minThreshold, int _maxThreshold) public {
        // set minimum temperature
        minTemp = _minThreshold;
        // set maximum temperature
        maxTemp = _maxThreshold;
    }

    /**
    * System should return the defined minimum and maximum threshold.
    */
    function GetTemperatureThreshold() public view returns (int, int) {
       // return defined minimum and maximum temperature 
       return (minTemp, maxTemp);
    }

}