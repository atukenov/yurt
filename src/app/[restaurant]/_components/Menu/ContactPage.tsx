"use client";

import Button from "#components/base/Button";
import { FaEnvelope, FaMapMarkerAlt } from "react-icons/fa";

import { useRestaurant } from "#components/context/useContext";
import NoContent from "#components/layout/NoContent";

import "./contactPage.scss";

const ContactPage = () => {
  const { restaurant, selectedAddress } = useRestaurant();

  if (!selectedAddress) {
    return (
      <div className="contactPage">
        <NoContent
          label="Please select an address to view contact information"
          animationName="GhostNoContent"
        />
      </div>
    );
  }

  const handleEmail = () => {
    if (restaurant?.email) {
      window.location.href = `mailto:${restaurant.email}`;
    }
  };

  return (
    <div className="contactPage">
      <div className="contactHeader">
        <h2>Contact Us</h2>
        <p>Get in touch with us. We're here to help!</p>
      </div>

      <div className="contactContent">
        <div className="contactCard mainCard">
          <div className="cardHeader">
            <FaMapMarkerAlt className="icon" />
            <h3>Location</h3>
          </div>
          <div className="cardBody">
            <p className="address">{selectedAddress}</p>
          </div>
        </div>

        {restaurant?.email && (
          <div className="contactCard">
            <div className="cardHeader">
              <FaEnvelope className="icon" />
              <h3>Email</h3>
            </div>
            <div className="cardBody">
              <p className="email">{restaurant.email}</p>
              <Button label="Send Email" icon="f0e0" onClick={handleEmail} />
            </div>
          </div>
        )}
      </div>

      <div className="contactFooter">
        <div className="infoBox">
          <h4>{restaurant?.profile?.name || restaurant?.username}</h4>
          <p>We look forward to serving you!</p>
        </div>
      </div>
    </div>
  );
};

export default ContactPage;
