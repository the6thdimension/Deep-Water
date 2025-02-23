import os
import cv2
import numpy as np

collection = os.getcwd()


for i, filename in enumerate(os.listdir(collection)):
    if (".png" in filename):
        # src = cv2.imread(filename, cv2.IMREAD_UNCHANGED)
        # # print(src.shape)
        image = cv2.imread(filename)

        # red_channel = src[:,:,2]

        (B, G, R) = cv2.split(image)
        # show each channel individually
        # cv2.imshow("Red", R)
        # cv2.imshow("Green", G)
        # cv2.imshow("Blue", B)
        # cv2.waitKey(0)

        # print(filename[:-4] + "AO.png")
        cv2.imwrite(filename[:-4] + "AO.png", R)
        cv2.imwrite(filename[:-4] + "ROUGH.png", G) 
        cv2.imwrite(filename[:-4] + "METAL.png", B) 



