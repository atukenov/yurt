import Lottie from "#components/base/Lottie";
import { getAnimSrc } from "#utils/constants/common";

import "./noContent.scss";

const NoContent = (props: TNoContentProps) => {
  const { animationName, label, size = 250, speed } = props;
  return (
    <div className="noContent">
      <div>
        <div style={{ width: `${size}px`, height: `${size}px` }}>
          <Lottie src={getAnimSrc(animationName)} speed={speed} />
        </div>
        {label && <p>{label}</p>}
      </div>
    </div>
  );
};

export default NoContent;

export type TNoContentProps = {
  animationName: string;
  label: string;
  size?: number;
  speed?: number;
};
